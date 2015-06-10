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
using System.Data.SQLite;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Amazon.CognitoSync.SyncManager.Internal
{
    public partial class SQLiteLocalStorage : ILocalStorage
    {

        //datetime is converted to ticks and stored as string

        private static SQLiteConnection connection;

        #region dispose methods

        public virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                connection.Close();
                connection.Dispose();
            }
        }

        #endregion

        #region helper methods


        private static void SetupDatabase()
        {
            SQLiteConnection.CreateFile(DB_FILE_NAME);
            connection = new SQLiteConnection("Data Source=test.db;Version=3;");
            connection.Open();
            string createDatasetTable = "CREATE TABLE IF NOT EXISTS " + TABLE_DATASETS + "("
                        + DatasetColumns.IDENTITY_ID + " TEXT NOT NULL,"
                        + DatasetColumns.DATASET_NAME + " TEXT NOT NULL,"
                        + DatasetColumns.CREATION_TIMESTAMP + " TEXT DEFAULT '0',"
                        + DatasetColumns.LAST_MODIFIED_TIMESTAMP + " TEXT DEFAULT '0',"
                        + DatasetColumns.LAST_MODIFIED_BY + " TEXT,"
                        + DatasetColumns.STORAGE_SIZE_BYTES + " INTEGER DEFAULT 0,"
                        + DatasetColumns.RECORD_COUNT + " INTEGER DEFAULT 0,"
                        + DatasetColumns.LAST_SYNC_COUNT + " INTEGER NOT NULL DEFAULT 0,"
                        + DatasetColumns.LAST_SYNC_TIMESTAMP + " INTEGER DEFAULT '0',"
                        + DatasetColumns.LAST_SYNC_RESULT + " TEXT,"
                        + "UNIQUE (" + DatasetColumns.IDENTITY_ID + ", "
                        + DatasetColumns.DATASET_NAME + ")"
                        + ")";

            using (var command = new SQLiteCommand(createDatasetTable, connection))
            {
                command.ExecuteNonQuery();
            }

            string createRecordsTable = "CREATE TABLE IF NOT EXISTS " + TABLE_RECORDS + "("
                        + RecordColumns.IDENTITY_ID + " TEXT NOT NULL,"
                        + RecordColumns.DATASET_NAME + " TEXT NOT NULL,"
                        + RecordColumns.KEY + " TEXT NOT NULL,"
                        + RecordColumns.VALUE + " TEXT,"
                        + RecordColumns.SYNC_COUNT + " INTEGER NOT NULL DEFAULT 0,"
                        + RecordColumns.LAST_MODIFIED_TIMESTAMP + " TEXT DEFAULT '0',"
                        + RecordColumns.LAST_MODIFIED_BY + " TEXT,"
                        + RecordColumns.DEVICE_LAST_MODIFIED_TIMESTAMP + " TEXT DEFAULT '0',"
                        + RecordColumns.MODIFIED + " INTEGER NOT NULL DEFAULT 1,"
                        + "UNIQUE (" + RecordColumns.IDENTITY_ID + ", " + RecordColumns.DATASET_NAME
                        + ", " + RecordColumns.KEY + ")"
                        + ")";

            using (var command = new SQLiteCommand(createRecordsTable, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        internal void CreateDatasetHelper(string query, params object[] parameters)
        {
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = query;
                BindData(command, parameters);
                command.ExecuteNonQuery();
            }
        }

        internal DatasetMetadata GetMetadataHelper(string identityId, string datasetName)
        {
            string query = DatasetColumns.BuildQuery(
                    DatasetColumns.IDENTITY_ID + " = ? AND " +
                        DatasetColumns.DATASET_NAME + " = ?"
                    );

            DatasetMetadata metadata = null;

            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = query;
                BindData(command, identityId, datasetName);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.HasRows && reader.Read())
                    {
                        metadata = SqliteStmtToDatasetMetadata(reader);
                    }
                }
            }
            return metadata;
        }

        internal List<DatasetMetadata> GetDatasetMetadataHelper(string query, params string[] parameters)
        {
            List<DatasetMetadata> datasetMetadataList = new List<DatasetMetadata>();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = query;
                BindData(command, parameters);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.HasRows && reader.Read())
                    {
                        datasetMetadataList.Add(SqliteStmtToDatasetMetadata(reader));
                    }
                }
            }

            return datasetMetadataList;
        }

        internal Record GetRecordHelper(string query, params string[] parameters)
        {
            Record record = null;
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = query;
                BindData(command, parameters);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.HasRows && reader.Read())
                    {
                        record = SqliteStmtToRecord(reader);
                    }
                }
            }
            return record;
        }

        internal List<Record> GetRecordsHelper(string query, params string[] parameters)
        {
            List<Record> records = new List<Record>();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = query;
                BindData(command, parameters);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.HasRows && reader.Read())
                    {
                        records.Add(SqliteStmtToRecord(reader));
                    }
                }
            }
            return records;
        }

        internal long GetLastSyncCountHelper(string query, params string[] parameters)
        {
            long lastSyncCount = 0;
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = query;
                BindData(command, parameters);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.HasRows && reader.Read())
                    {
                        var nvc = reader.GetValues();
                        lastSyncCount = long.Parse(nvc[DatasetColumns.LAST_SYNC_COUNT]);
                    }
                }
            }
            return lastSyncCount;
        }

        internal List<Record> GetModifiedRecordsHelper(string query, params object[] parameters)
        {
            List<Record> records = new List<Record>();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = query;
                BindData(command, parameters);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.HasRows && reader.Read())
                    {
                        records.Add(SqliteStmtToRecord(reader));
                    }
                }
            }
            return records;
        }

        internal void ExecuteMultipleHelper(List<Statement> statements)
        {
            using (var transaction = connection.BeginTransaction())
            {

                foreach (var stmt in statements)
                {
                    string query = stmt.Query.TrimEnd();
                    //transaction statements should end with a semi-colon, so if there is no semi-colon then append it in the end
                    if (!query.EndsWith(";"))
                    {
                        query += ";";
                    }
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = query;
                        BindData(command, stmt.Parameters);
                        command.Transaction = transaction;
                        command.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }

        internal void UpdateLastSyncCountHelper(string query, params object[] parameters)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                BindData(command, parameters);
                command.ExecuteNonQuery();
            }
        }

        internal void UpdateLastModifiedTimestampHelper(string query, params object[] parameters)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                BindData(command, parameters);
                command.ExecuteNonQuery();
            }
        }

        internal void UpdateAndClearRecord(string identityId, string datasetName, Record record)
        {
            lock (sqlite_lock)
            {
                string updateAndClearQuery = RecordColumns.BuildQuery(
                    RecordColumns.IDENTITY_ID + " = @whereIdentityId AND " +
                    RecordColumns.DATASET_NAME + " = @whereDatasetName AND " +
                    RecordColumns.KEY + " = @whereKey "
                );
                bool recordsFound = false;

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = updateAndClearQuery;
                    BindData(command, identityId, datasetName, record.Key);
                    command.ExecuteNonQuery();
                }

                if (recordsFound)
                {
                    string updateRecordQuery =
                    RecordColumns.BuildUpdate(
                        new string[] {
                            RecordColumns.VALUE,
                            RecordColumns.SYNC_COUNT,
                            RecordColumns.MODIFIED
                        },
                    RecordColumns.IDENTITY_ID + " = @whereIdentityId AND " +
                        RecordColumns.DATASET_NAME + " = @whereDatasetName AND " +
                        RecordColumns.KEY + " = @whereKey "
                    );

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = updateRecordQuery;
                        BindData(command, record.Value, record.SyncCount, record.IsModified ? 1 : 0, identityId, datasetName, record.Key);
                        command.ExecuteNonQuery();
                    }
                }
                else
                {
                    string insertRecord = RecordColumns.BuildInsert();
                    using (var command = new SQLiteCommand(insertRecord, connection))
                    {
                        BindData(command, identityId, datasetName, record.Key, record.Value, record.SyncCount, record.LastModifiedDate, record.LastModifiedBy, record.DeviceLastModifiedDate, record.IsModified ? 1 : 0);
                        command.ExecuteNonQuery();
                    }
                }

            }
        }

        #endregion

        #region private methods
        private static void BindData(SQLiteCommand command, params object[] parameters)
        {
            string query = command.CommandText;
            int count = 0;
            foreach (Match match in Regex.Matches(query, "(\\@\\w+) "))
            {
                command.Parameters.Add(new SQLiteParameter(match.Groups[1].Value, parameters[count]));
                count++;
            }
        }

        private static DatasetMetadata SqliteStmtToDatasetMetadata(SQLiteDataReader reader)
        {
            var nvc = reader.GetValues();
            return new DatasetMetadata(
                nvc[DatasetColumns.DATASET_NAME],
                new DateTime(long.Parse(nvc[DatasetColumns.CREATION_TIMESTAMP])),
                new DateTime(long.Parse(nvc[DatasetColumns.LAST_MODIFIED_TIMESTAMP])),
                nvc[DatasetColumns.LAST_MODIFIED_BY],
                long.Parse(nvc[DatasetColumns.STORAGE_SIZE_BYTES]),
                long.Parse(nvc[DatasetColumns.RECORD_COUNT])
            );
        }

        private static Record SqliteStmtToRecord(SQLiteDataReader reader)
        {
            var nvc = reader.GetValues();
            return new Record(nvc[RecordColumns.KEY], nvc[RecordColumns.VALUE],
                               int.Parse(nvc[RecordColumns.SYNC_COUNT]), new DateTime(long.Parse(nvc[RecordColumns.LAST_MODIFIED_TIMESTAMP])),
                               nvc[RecordColumns.LAST_MODIFIED_BY], new DateTime(long.Parse(nvc[RecordColumns.DEVICE_LAST_MODIFIED_TIMESTAMP])),
                               int.Parse(nvc[RecordColumns.MODIFIED]) == 1);
        }
        #endregion

    }
}

