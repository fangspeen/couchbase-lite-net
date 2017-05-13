﻿// 
// IDictionaryObject.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System.Collections.Generic;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface representing a writeable key value collection
    /// </summary>
    public interface IDictionaryObject : IReadOnlyDictionary, IDictionaryFragment
    {
        #region Properties

        /// <summary>
        /// Gets the value of the given key, or lack thereof,
        /// wrapped inside of a <see cref="Fragment"/>
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value of the given key, or lack thereof</returns>
        new Fragment this[string key] { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the value of a given key as an <see cref="IArray"/>
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or <c>null</c></returns>
        new IArray GetArray(string key);

        /// <summary>
        /// Gets the value of a given key as a dictionary
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or <c>null</c></returns>
        new IDictionaryObject GetDictionary(string key);

        /// <summary>
        /// Removes the given key from this dictionary
        /// </summary>
        /// <param name="key">The key to remove</param>
        /// <returns>Itself for further processing</returns>
        IDictionaryObject Remove(string key);

        /// <summary>
        /// Sets the given key to the given value
        /// </summary>
        /// <param name="key">The key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>Itself for further processing</returns>
        IDictionaryObject Set(string key, object value);

        /// <summary>
        /// Replaces the contents of this dictionary with the contents of the
        /// given one
        /// </summary>
        /// <param name="dictionary">The dictionary to replace the current contents with</param>
        /// <returns>Itself for further processing</returns>
        IDictionaryObject Set(IDictionary<string, object> dictionary);

        #endregion
    }
}
