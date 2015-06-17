//
// Copyright 2014-2015 Amazon.com, 
// Inc. or its affiliates. All Rights Reserved.
// 
// Licensed under the Amazon Software License (the "License"). 
// You may not use this file except in compliance with the 
// License. A copy of the License is located at
// 
//     http://aws.amazon.com/asl/
// 
// or in the "license" file accompanying this file. This file is 
// distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, express or implied. See the License 
// for the specific language governing permissions and 
// limitations under the License.
//
using Amazon.Runtime.Internal.Util;
using System;
using System.Collections.Generic;

namespace Amazon.CognitoSync.SyncManager.Internal
{
    /// <summary>
    /// An implementation for <see cref="Amazon.CognitoSync.SyncManager.ILocalStorage"/> 
    /// This implementation does not persist the information to disk. If you want to use 
    /// persistant storage option, then use <see cref="Amazon.CognitoSync.SyncManager.Internal.SQLiteLocalStorage"/>
    /// </summary>
    public partial class InMemoryStorage : ILocalStorage
    {
        private static object _lock = new object();
        private ILogger _logger;

        /// <summary>
        /// Creates an instance of InMemoryStorage
        /// </summary>
        public InMemoryStorage()
        {
            _logger = Logger.GetLogger(this.GetType());
        }

        #region inmemory datastructures

        static Dictionary<string, DatasetMetadataObject> MetadataStore;
        static Dictionary<string, Dictionary<string, RecordObject>> DatasetStore;

        static InMemoryStorage()
        {
            MetadataStore = new Dictionary<string, DatasetMetadataObject>();
            DatasetStore = new Dictionary<string, Dictionary<string, RecordObject>>();
        }

        static string MakeKey(string identityId, string datasetName)
        {
            return identityId + '.' + datasetName;
        }

        class DatasetMetadataObject
        {
            private DateTime? _lastModifiedTimestampUTC = null;
            private DateTime? _lastSyncTimestamp = null;

            public string IdentityId { get; set; }

            public string DatasetName { get; set; }

            public DateTime? CreationTimestamp { get; set; }

            public DateTime? LastModifiedTimestamp
            {
                get
                {
                    return InMemoryStorage.ConvertToLocalTime(_lastModifiedTimestampUTC);
                }
                set
                {
                    _lastModifiedTimestampUTC = InMemoryStorage.ConvertToUTCTime(value);
                }
            }

            public string LastModifiedBy { get; set; }

            public long StorageSizeBytes { get; set; }

            public long RecordCount { get; set; }

            public long LastSyncCount { get; set; }

            public DateTime? LastSyncTimestamp
            {
                get
                {
                    return InMemoryStorage.ConvertToLocalTime(_lastSyncTimestamp);
                }
                set
                {
                    _lastSyncTimestamp = InMemoryStorage.ConvertToUTCTime(value);
                }
            }

            public string LastSyncResult { get; set; }

            public DatasetMetadata ConvertToDatasetMetadata()
            {
                return new DatasetMetadata(this.DatasetName, this.CreationTimestamp, this.LastModifiedTimestamp, this.LastModifiedBy, this.StorageSizeBytes, this.RecordCount);
            }
        }

        private void UpdateLastModifiedTimestamp(string identityId, string datasetName)
        {
            lock (_lock)
            {
                InMemoryStorage.MetadataStore[MakeKey(identityId, datasetName)].LastModifiedTimestamp = DateTime.Now;
            }
        }

        private static DateTime? ConvertToLocalTime(DateTime? utcTimestamp)
        {
            return utcTimestamp == null ? utcTimestamp : utcTimestamp.Value.ToLocalTime();
        }

        private static DateTime? ConvertToUTCTime(DateTime? localTimestamp)
        {
            return localTimestamp == (DateTime?)null ? localTimestamp : localTimestamp.Value.ToUniversalTime();
        }

        class RecordObject
        {
            private DateTime? _lastModifiedTimestampInUTC = null;
            private DateTime? _deviceLastModifiedTimestamp = null;

            public string IdentityId { get; set; }

            public string DatasetName { get; set; }

            public string Key { get; set; }

            public string Value { get; set; }

            public long SyncCount { get; set; }

            public DateTime? LastModifiedTimestamp
            {
                get
                {
                    return InMemoryStorage.ConvertToLocalTime(_lastModifiedTimestampInUTC);
                }
                set
                {
                    _lastModifiedTimestampInUTC = InMemoryStorage.ConvertToUTCTime(value);
                }
            }

            public string LastModifiedBy { get; set; }

            public DateTime? DeviceLastModifiedTimestamp
            {
                get
                {
                    return InMemoryStorage.ConvertToLocalTime(_deviceLastModifiedTimestamp);
                }
                set
                {
                    _deviceLastModifiedTimestamp = InMemoryStorage.ConvertToUTCTime(value);
                }
            }

            public bool IsModified { get; set; }
        }

        private RecordObject GetRecordInternal(string identityId, string datasetName, string key)
        {
            lock (_lock)
            {
                string datasetKey = MakeKey(identityId, datasetName);
                if (!InMemoryStorage.DatasetStore.ContainsKey(datasetKey))
                    throw new NullReferenceException("Dataset is null");

                return (InMemoryStorage.DatasetStore[datasetKey].ContainsKey(key)) ? InMemoryStorage.DatasetStore[datasetKey][key] : null;
            }
        }

        private Record ConvertToRecord(RecordObject record)
        {
            if (record == null)
                return null;

            return new Record(record.Key, record.Value, record.SyncCount, record.LastModifiedTimestamp, record.LastModifiedBy, record.DeviceLastModifiedTimestamp, record.IsModified);
        }


        #endregion

        #region implemented abstract members of LocalStorage

        /// <summary>
        /// Create a dataset 
        /// </summary>
        /// <param name="identityId">Identity Id</param>
        /// <param name="datasetName">Dataset name.</param>
        public void CreateDataset(string identityId, string datasetName)
        {
            lock (_lock)
            {
                if (GetDatasetMetadata(identityId, datasetName) == null)
                {
                    string datasetKey = MakeKey(identityId, datasetName);
                    _logger.InfoFormat("CreateDataset = {0}", datasetKey);
                    DatasetMetadataObject newMetadata = new DatasetMetadataObject
                    {
                        IdentityId = identityId,
                        DatasetName = datasetName,
                        CreationTimestamp = DateTime.Now,
                        LastModifiedTimestamp = DateTime.Now
                    };
                    InMemoryStorage.MetadataStore[datasetKey] = newMetadata;
                    InMemoryStorage.DatasetStore[datasetKey] = new Dictionary<string, RecordObject>();
                }
            }
        }

        /// <summary>
        /// Retrieves the string value of a key in dataset. The value can be null
        /// when the record doesn't exist or is marked as deleted.
        /// </summary>
        /// <returns>string value of the record, or null if not present or deleted.</returns>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        /// <param name="key">record key.</param>
        public string GetValue(string identityId, string datasetName, string key)
        {
            lock (_lock)
            {
                RecordObject record = GetRecordInternal(identityId, datasetName, key);
                return record == null ? null : record.Value;
            }
        }

        /// <summary>
        /// Puts the value of a key in dataset. If a new value is assigned to the
        /// key, the record is marked as dirty. If the value is null, then the record
        /// is marked as deleted. The changed record will be synced with remote
        /// storage.
        /// </summary>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        /// <param name="key">record key.</param>
        /// <param name="value">string value. If null, the record is marked as deleted.</param>
        public void PutValue(string identityId, string datasetName, string key, string value)
        {
            lock (_lock)
            {
                PutValueInternal(identityId, datasetName, key, value);
                UpdateLastModifiedTimestamp(identityId, datasetName);
            }
        }

        /// <summary>
        /// Retrieves a key-value map from dataset, excluding marked as deleted
        /// values.
        /// </summary>
        /// <returns>a key-value map of all but deleted values.</returns>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        public Dictionary<string, string> GetValueMap(string identityId, string datasetName)
        {
            lock (_lock)
            {
                Dictionary<string, RecordObject> inmemoryDatasetMap = InMemoryStorage.DatasetStore[MakeKey(identityId, datasetName)];
                Dictionary<string, string> valuesMap = new Dictionary<string, string>();

                foreach (KeyValuePair<string, RecordObject> record in inmemoryDatasetMap)
                {
                    valuesMap[record.Key] = record.Value.Value;
                }
                return valuesMap;
            }
        }

        /// <summary>
        /// Puts a key-value map into a dataset. This is optimized for batch
        /// operation. It's the preferred way to put a list of records into dataset.
        /// </summary>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        /// <param name="values">a key-value map.</param>
        public void PutAllValues(string identityId, string datasetName, IDictionary<string, string> values)
        {
            lock (_lock)
            {

                string datasetKey = MakeKey(identityId, datasetName);
                if (!InMemoryStorage.DatasetStore.ContainsKey(datasetKey))
                    throw new KeyNotFoundException("Dataset");

                foreach (KeyValuePair<string, string> valueKV in values)
                    PutValueInternal(identityId, datasetName, valueKV.Key, valueKV.Value);
                UpdateLastModifiedTimestamp(identityId, datasetName);
            }
        }

        /// <summary>
        /// Gets a raw record from local store. If the dataset/key combo doesn't
        /// // exist, null will be returned.
        /// </summary>
        /// <returns>a Record object if found, null otherwise.</returns>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        /// <param name="key">Key for the record.</param>
        public Record GetRecord(string identityId, string datasetName, string key)
        {
            lock (_lock)
            {

                string datasetKey = MakeKey(identityId, datasetName);
                if (!InMemoryStorage.DatasetStore.ContainsKey(datasetKey))
                    throw new KeyNotFoundException("Dataset");

                return ConvertToRecord(GetRecordInternal(identityId, datasetName, key));
            }
        }

        /// <summary>
        /// Gets a list of all records.
        /// </summary>
        /// <returns>A list of records which have been updated since lastSyncCount.</returns>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        public List<Record> GetRecords(string identityId, string datasetName)
        {
            lock (_lock)
            {
                string datasetKey = MakeKey(identityId, datasetName);
                if (!InMemoryStorage.DatasetStore.ContainsKey(datasetKey))
                    throw new KeyNotFoundException("Dataset");

                Dictionary<string, RecordObject> inmemoryDatasetMap = InMemoryStorage.DatasetStore[datasetKey];
                List<Record> records = new List<Record>();

                foreach (KeyValuePair<string, RecordObject> recordKV in inmemoryDatasetMap)
                {
                    records.Add(ConvertToRecord(recordKV.Value));
                }
                return records;
            }
        }

        /// <summary>
        /// Retrieves a list of locally modified records since last successful sync
        /// operation.
        /// </summary>
        /// <returns>a list of locally modified records</returns>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        public List<Record> GetModifiedRecords(string identityId, string datasetName)
        {
            lock (_lock)
            {

                string datasetKey = MakeKey(identityId, datasetName);
                if (!InMemoryStorage.DatasetStore.ContainsKey(datasetKey))
                    throw new KeyNotFoundException("Dataset");

                Dictionary<string, RecordObject> inmemoryDatasetMap = InMemoryStorage.DatasetStore[datasetKey];
                List<Record> records = new List<Record>();

                foreach (KeyValuePair<string, RecordObject> recordKV in inmemoryDatasetMap)
                {
                    if (recordKV.Value.IsModified)
                        records.Add(ConvertToRecord(recordKV.Value));
                }
                return records;
            }
        }

        /// <summary>
        /// Puts a list of raw records into dataset.
        /// </summary>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        /// <param name="records">A list of Records.</param>
        public void PutRecords(string identityId, string datasetName, List<Record> records)
        {
            lock (_lock)
            {
                string datasetKey = MakeKey(identityId, datasetName);
                if (!InMemoryStorage.DatasetStore.ContainsKey(datasetKey))
                    throw new KeyNotFoundException("Dataset");

                Dictionary<string, RecordObject> inmemoryDatasetMap = InMemoryStorage.DatasetStore[datasetKey];
                RecordObject storedRecord;

                foreach (Record record in records)
                {
                    if (inmemoryDatasetMap.ContainsKey(record.Key))
                    {
                        storedRecord = inmemoryDatasetMap[record.Key];
                        storedRecord.Value = record.Value;
                        storedRecord.IsModified = record.IsModified;
                        storedRecord.SyncCount = record.SyncCount;
                        storedRecord.LastModifiedTimestamp = record.LastModifiedDate;
                        storedRecord.LastModifiedBy = record.LastModifiedBy;
                        storedRecord.DeviceLastModifiedTimestamp = record.DeviceLastModifiedDate;
                    }
                    else
                    {
                        storedRecord = new RecordObject
                        {
                            IdentityId = identityId,
                            DatasetName = datasetName,
                            Key = record.Key,
                            Value = record.Value,
                            SyncCount = record.SyncCount,
                            LastModifiedTimestamp = record.LastModifiedDate,
                            LastModifiedBy = record.LastModifiedBy,
                            DeviceLastModifiedTimestamp = record.DeviceLastModifiedDate,
                            IsModified = record.IsModified
                        };
                    }
                }
                UpdateLastModifiedTimestamp(identityId, datasetName);
            }
        }

        /// <summary>
        /// Puts a list of raw records into that dataset if 
        /// the local version hasn't changed (to be used in 
        /// synchronizations). 
        /// </summary> 
        /// <param name="identityId">Identity id.</param>
        /// <param name="datasetName">Dataset name.</param>
        /// <param name="localRecords">A list of records to check for changes.</param>
        public void ConditionallyPutRecords(string identityId, string datasetName, List<Record> records, List<Record> localRecords)
        {
            PutRecords(identityId, datasetName, records);
        }

        /// <summary>
        /// Gets a list of dataset's metadata information.
        /// </summary>
        /// <returns>a list of dataset metadata</returns>
        /// <param name="identityId">Identity identifier.</param>
        /// <exception cref="DataStorageException"></exception>
        public List<DatasetMetadata> GetDatasetMetadata(string identityId)
        {
            List<DatasetMetadata> metadataList = new List<DatasetMetadata>();
            foreach (KeyValuePair<string, DatasetMetadataObject> metadataKV in InMemoryStorage.MetadataStore)
            {
                if (metadataKV.Key.StartsWith(identityId + "."))
                {
                    metadataList.Add(metadataKV.Value.ConvertToDatasetMetadata());
                }
            }
            return metadataList;
        }

        /// <summary>
        /// Deletes a dataset. It clears all records in this dataset and marked it as
        /// deleted for future sync.
        /// </summary>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        /// <exception cref="DatasetNotFoundException"></exception>
        public void DeleteDataset(string identityId, string datasetName)
        {
            lock (_lock)
            {
                string datasetKey = MakeKey(identityId, datasetName);
                if (InMemoryStorage.DatasetStore.ContainsKey(datasetKey))
                    InMemoryStorage.DatasetStore.Remove(datasetKey);
                if (InMemoryStorage.MetadataStore.ContainsKey(datasetKey))
                {
                    DatasetMetadataObject inmemoryMetadata = InMemoryStorage.MetadataStore[datasetKey];
                    inmemoryMetadata.LastSyncCount = -1;
                    inmemoryMetadata.LastModifiedTimestamp = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// This is different from <see cref="DeleteDataset(String,String)"/>. Not only does it
        /// clears all records in the dataset, it also remove it from metadata table.
        /// It won't be visible in <see cref="GetDatasetMetadata(String,String)"/>.
        /// </summary>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        public void PurgeDataset(string identityId, string datasetName)
        {
            lock (_lock)
            {
                InMemoryStorage.DatasetStore.Remove(MakeKey(identityId, datasetName));
                InMemoryStorage.MetadataStore.Remove(MakeKey(identityId, datasetName));
            }
        }

        /// <summary>
        /// Retrieves the metadata of a dataset.
        /// </summary>
        /// <returns>The dataset metadata.</returns>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        /// <exception cref="DataStorageException"></exception>
        public DatasetMetadata GetDatasetMetadata(string identityId, string datasetName)
        {
            lock (_lock)
            {
                string datasetKey = MakeKey(identityId, datasetName);
                _logger.DebugFormat("GetDatasetMetadata = {0}" + datasetKey);
                return InMemoryStorage.MetadataStore.ContainsKey(datasetKey) ? InMemoryStorage.MetadataStore[datasetKey].ConvertToDatasetMetadata() : null;
            }
        }

        /// <summary>
        /// Retrieves the last sync count. This sync count is a counter that
        /// represents when the last sync happened. The counter should be updated on
        /// a successful sync.
        /// </summary>
        /// <returns>The last sync count.</returns>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        public long GetLastSyncCount(string identityId, string datasetName)
        {
            lock (_lock)
            {
                return InMemoryStorage.MetadataStore[MakeKey(identityId, datasetName)].LastSyncCount;
            }
        }

        /// <summary>
        /// Updates the last sync count after successful sync with the remote data
        /// store.
        /// </summary>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetName">Dataset name.</param>
        /// <param name="lastSyncCount">Last sync count.</param>
        public void UpdateLastSyncCount(string identityId, string datasetName, long lastSyncCount)
        {
            lock (_lock)
            {
                DatasetMetadataObject storedMetadata = InMemoryStorage.MetadataStore[MakeKey(identityId, datasetName)];
                storedMetadata.LastSyncCount = lastSyncCount;
                storedMetadata.LastSyncTimestamp = DateTime.Now;

            }
        }

        /// <summary>
        /// Wipes all locally cached data including dataset metadata and records. All
        /// opened dataset handler should not perform further operations to avoid
        /// inconsistent state.
        /// </summary>
        public void WipeData()
        {
            lock (_lock)
            {
                InMemoryStorage.MetadataStore.Clear();
                InMemoryStorage.DatasetStore.Clear();
            }
        }

        /// <summary>
        /// Reparents all datasets from old identity id to a new one.
        /// </summary>
        /// <param name="oldIdentityId">Old identity identifier.</param>
        /// <param name="newIdentityId">New identity identifier.</param>
        public void ChangeIdentityId(string oldIdentityId, string newIdentityId)
        {
            lock (_lock)
            {
                List<string> oldDatasetKeys = new List<string>();
                foreach (KeyValuePair<string, DatasetMetadataObject> metadataKV in InMemoryStorage.MetadataStore)
                {
                    if (metadataKV.Key.StartsWith(oldIdentityId + "."))
                    {
                        oldDatasetKeys.Add(metadataKV.Key);
                    }
                }

                foreach (string oldDatasetKey in oldDatasetKeys)
                {
                    DatasetMetadataObject metadataObject = InMemoryStorage.MetadataStore[oldDatasetKey];
                    string newDatasetKey = MakeKey(newIdentityId, metadataObject.DatasetName);
                    metadataObject.IdentityId = newIdentityId;
                    InMemoryStorage.MetadataStore[newDatasetKey] = metadataObject;
                    InMemoryStorage.MetadataStore.Remove(oldDatasetKey);

                    foreach (KeyValuePair<string, RecordObject> recordKV in InMemoryStorage.DatasetStore[oldDatasetKey])
                    {
                        recordKV.Value.IdentityId = newIdentityId;
                    }
                    InMemoryStorage.DatasetStore[newDatasetKey] = InMemoryStorage.DatasetStore[oldDatasetKey];
                    InMemoryStorage.DatasetStore.Remove(oldDatasetKey);
                }
            }
        }

        /// <summary>
        /// Updates local dataset metadata
        /// </summary>
        /// <param name="identityId">Identity identifier.</param>
        /// <param name="datasetMetadataList">Dataset metadata.</param>
        public void UpdateDatasetMetadata(string identityId, List<DatasetMetadata> datasetMetadataList)
        {
            lock (_lock)
            {
                foreach (DatasetMetadata metadata in datasetMetadataList)
                {
                    string datasetkey = MakeKey(identityId, metadata.DatasetName);
                    if (InMemoryStorage.MetadataStore.ContainsKey(datasetkey))
                    {
                        DatasetMetadataObject storedMetadata = InMemoryStorage.MetadataStore[datasetkey];
                        storedMetadata.CreationTimestamp = metadata.CreationDate;
                        storedMetadata.LastModifiedTimestamp = metadata.LastModifiedDate;
                        storedMetadata.LastModifiedBy = metadata.LastModifiedBy;
                        storedMetadata.RecordCount = metadata.RecordCount;
                        storedMetadata.StorageSizeBytes = metadata.StorageSizeBytes;
                    }
                    else
                    {
                        DatasetMetadataObject newMetadata = new DatasetMetadataObject
                        {
                            IdentityId = identityId,
                            DatasetName = metadata.DatasetName,
                            CreationTimestamp = metadata.CreationDate,
                            LastModifiedTimestamp = metadata.LastModifiedDate,
                            RecordCount = metadata.RecordCount,
                            StorageSizeBytes = metadata.StorageSizeBytes,
                            LastSyncCount = 0,
                            LastSyncTimestamp = null,
                            LastSyncResult = null
                        };
                        InMemoryStorage.MetadataStore[datasetkey] = newMetadata;
                    }
                }
            }
        }
        #endregion

        #region private methods
        private void PutValueInternal(string identityId, string datasetName, string key, string value)
        {
            lock (_lock)
            {
                string datasetKey = MakeKey(identityId, datasetName);
                RecordObject record = GetRecordInternal(identityId, datasetName, key);

                if (record != null && record.Value.Equals(value, StringComparison.Ordinal))
                {
                    return;
                }

                if (record == null)
                {
                    record = new RecordObject
                    {
                        IdentityId = identityId,
                        DatasetName = datasetName,
                        Key = key,
                        Value = value,
                        SyncCount = 0,
                        LastModifiedTimestamp = DateTime.Now,
                        LastModifiedBy = string.Empty,
                        DeviceLastModifiedTimestamp = null,
                        IsModified = true
                    };
                }
                else
                {
                    record.Value = value;
                    record.IsModified = true;
                    record.LastModifiedTimestamp = DateTime.Now;
                }

                InMemoryStorage.DatasetStore[datasetKey][key] = record;
            }
        }
        #endregion

    }
}

