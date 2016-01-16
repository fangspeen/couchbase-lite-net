﻿//
// CookieStore.cs
//
// Author:
//     Pasin Suriyentrakorn  <pasin@couchbase.com>
//
// Copyright (c) 2014 Couchbase Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if !NET_3_5
using System.Net;
using StringEx = System.String;
#else
using System.Net.Couchbase;
#endif

namespace Couchbase.Lite.Util
{

    /// <summary>
    /// An object that holds and serializes cookies
    /// </summary>
    public class CookieStore : CookieContainer
    {

        #region Constants

        private const string TAG = "CookieStore";
        private const string FileName = "cookies.json";
        private const string LOCAL_DOC_KEY_PREFIX = "cbl_cookie_storage";

        #endregion

        #region Variables

        private readonly object locker = new object();
        private readonly DirectoryInfo directory;
        private readonly Database _db;
        private readonly string _storageKey;
        private HashSet<Uri> _cookieUriReference = new HashSet<Uri>();

        #endregion

        #region Properties

        private string LocalDocKey 
        {
            get {
                return String.Format("{0}_{1}", LOCAL_DOC_KEY_PREFIX, _storageKey);
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Convenience constructor
        /// </summary>
        public CookieStore() : this (null) { }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="directory">The directory to serialize the cookies to</param>
        [Obsolete("Use the database constructor")]
        public CookieStore(string directory) 
        {
            if (directory != null) {
                this.directory = new DirectoryInfo(directory);
            }

            DeserializeFromDisk();
        }

        public CookieStore(Database db, string storageKey)
        {
            _db = db;
            _storageKey = storageKey;
            DeserializeFromDisk();
            DeserializeFromDB();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Add the specified cookies, force overrides CookieCollection
        /// </summary>
        /// <param name="cookies">The cookies to add</param>
        public new void Add(CookieCollection cookies)
        {
            foreach (Cookie cookie in cookies) {
                Add(cookie, false);
            }

            Save();
        }

        /// <summary>
        /// Add the specified cookie, force overrides CookieCollection
        /// </summary>
        /// <param name="cookie">The cookie to add</param>
        public new void Add(Cookie cookie)
        {
            Add(cookie, true);
        }

        /// <summary>
        /// Delete the cookie with the specified uri and name.
        /// </summary>
        /// <param name="uri">The uri of the cookie.</param>
        /// <param name="name">The name of the cookie.</param>
        public void Delete(Uri uri, string name)
        {
            if (uri == null || name == null) {
                return;
            }

            lock (locker) {
                var delete = false;
                var cookies = GetCookies(uri);
                foreach (Cookie cookie in cookies) {
                    if (name.Equals(cookie.Name)) {
                        cookie.Discard = true;
                        cookie.Expired = true;
                        cookie.Expires = DateTime.Now.Subtract(TimeSpan.FromDays(2));

                        if (!delete) {
                            delete = true;
                        }
                    }
                }

                if (delete) {
                    // Trigger container cookie list refreshment
                    GetCookies(uri);
                    Save();
                }
            }
        }

        /// <summary>
        /// Saves the cookies to disk
        /// </summary>
        public void Save()
        {
            lock (locker) {
                PruneExpiredCookies();
                if (_db != null) {
                    SerializeToDB();
                } else {
                    SerializeToDisk();
                }
            }
        }

        #endregion

        #region Private Methods

        private void PruneExpiredCookies()
        {
            foreach (var uri in _cookieUriReference) {
                var found = false;
                var collection = GetCookies(uri);
                for (int i = collection.Count - 1; i >= 0; i--) {
                    var cookie = collection[i];
                    if (IsNotSessionOnly(cookie) && (cookie.Expired || cookie.Expires < DateTime.Now)) {
                        cookie.Expired = true;
                        found = true;
                    }
                }

                if (found) {
                    //Refresh
                    GetCookies(uri);
                }
            }    
        }

        private void Add(Cookie cookie, bool save)
        {
            var urlString = String.Format("http://{0}{1}", cookie.Domain.TrimStart('.'), cookie.Path);
            var url = new Uri(urlString);
            _cookieUriReference.Add(url);

            var existing = GetCookies(url);
            foreach(Cookie existingCookie in existing) {
                if (existingCookie.Name == cookie.Name && existingCookie.Domain == cookie.Domain 
                    && existingCookie.Path == cookie.Path) {
                    existingCookie.Expired = true;
                    break;
                }
            }

            base.Add(cookie);

            if (save) {
                Save();
            }
        }

        private string GetSaveCookiesFilePath()
        {
            if (directory == null) {
                return null;
            }

            if (!directory.Exists) {
                directory.Create();
                directory.Refresh();
            }

            return Path.Combine(directory.FullName, FileName);
        }

        private bool IsNotSessionOnly(Cookie cookie)
        {
            return cookie.Expires != DateTime.MinValue;
        }

        private void SerializeToDisk()
        {
            var filePath = GetSaveCookiesFilePath();
            if (StringEx.IsNullOrWhiteSpace(filePath)) {
                return;
            }

            List<Cookie> aggregate = new List<Cookie>();
            foreach (var uri in _cookieUriReference) {
                var collection = GetCookies(uri);
                aggregate.AddRange(collection.Cast<Cookie>().Where(IsNotSessionOnly));
            }

            using (var writer = new StreamWriter(filePath)) {
                var json = Manager.GetObjectMapper().WriteValueAsString(aggregate);
                writer.Write(json);
            }
        }

        private void DeserializeFromDisk()
        {
            var filePath = GetSaveCookiesFilePath();
            if (StringEx.IsNullOrWhiteSpace(filePath)) {
                return;
            }

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) {
                return;
            }

            using (var reader = new StreamReader(filePath)) {
                var json = reader.ReadToEnd();

                var cookies = Manager.GetObjectMapper().ReadValue<IList<Cookie>>(json);
                cookies = cookies ?? new List<Cookie>();

                foreach (Cookie cookie in cookies) {
                    Add(cookie, false);
                }
            }
        }

        private void SerializeToDB()
        {
            if (_db == null || _storageKey == null) {
                Log.V(TAG, "Database or storage key null, so skipping serialization");
                return;
            }

            var key = LocalDocKey;
            List<Cookie> aggregate = new List<Cookie>();
            foreach (var uri in _cookieUriReference) {
                var collection = GetCookies(uri);
                aggregate.AddRange(collection.Cast<Cookie>().Where(IsNotSessionOnly));
            }

            _db.PutLocalCheckpointDoc(key, aggregate);
        }

        private void DeserializeFromDB()
        {
            if (_db == null || _storageKey == null) {
                Log.V(TAG, "Database or storage key null, so skipping deserialization");
                return;
            }

            var key = LocalDocKey;
            var val = _db.GetLocalCheckpointDocValue(key).AsList<Cookie>();
            if (val == null) {
                return;
            }

            foreach (var cookie in val) {
                Add(cookie, false);
            }
        }

        #endregion
    }
}

