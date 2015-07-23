using Amazon.CognitoSync.SyncManager.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Data.SQLite;
using System;
using System.Collections.Generic;

namespace AWSSDK_DotNet.UnitTests
{
    [TestClass]
    public class SQLiteTests
    {
        static string DB_FILE_NAME = "aws_cognito_sync.db";

        [TestCleanup]
        public void Cleaup()
        {
            //drop all the tables from the db
            var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CognitoSync", DB_FILE_NAME);

            using (SQLiteConnection connection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", filePath)))
            {
                connection.Open();

                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DROP TABLE records";
                    cmd.ExecuteNonQuery();
                }

                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DROP TABLE datasets";
                    cmd.ExecuteNonQuery();
                }

                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DROP TABLE kvstore";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        [TestMethod]
        [TestCategory("Sqlite")]
        public void SqliteInitializationTest()
        {
            var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CognitoSync", DB_FILE_NAME);

            using (SQLiteLocalStorage storage = new SQLiteLocalStorage())
            { }

            using (SQLiteConnection connection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", filePath)))
            {
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                var reader = cmd.ExecuteReader();

                var tableName = new List<string>();

                while (reader.Read())
                {
                    tableName.Add(reader.GetString(0));
                }

                Assert.IsTrue(tableName.Count == 3);
                Assert.IsTrue(tableName.Contains("datasets"));
                Assert.IsTrue(tableName.Contains("records"));
                Assert.IsTrue(tableName.Contains("kvstore"));
                connection.Close();
            }
        }

        [TestMethod]
        [TestCategory("Sqlite")]
        public void SQliteDatasetsTests()
        {
            var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CognitoSync", DB_FILE_NAME);

            string randomId = "old";
            string randomDataset = Guid.NewGuid().ToString();
            using (SQLiteLocalStorage storage = new SQLiteLocalStorage())
            {
                storage.WipeData();
                storage.CreateDataset(randomId, randomDataset);
                storage.PutValue(randomId, randomDataset, "Voldemort", "He who must not be named");

                using (SQLiteConnection connection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", filePath)))
                {
                    connection.Open();

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "select count(*) from datasets where dataset_name = @dataset_name and identity_id = @identity_id ";
                        cmd.Parameters.Add(new SQLiteParameter("@dataset_name", randomDataset));
                        cmd.Parameters.Add(new SQLiteParameter("@identity_id", randomId));
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var count = reader.GetInt32(0);
                                Assert.IsTrue(count == 1);
                            }
                            else
                            {
                                Assert.Fail();
                            }
                        }
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "select count(*) from records where dataset_name = @dataset_name and identity_id = @identity_id ";
                        cmd.Parameters.Add(new SQLiteParameter("@dataset_name", randomDataset));
                        cmd.Parameters.Add(new SQLiteParameter("@identity_id", randomId));
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var count = reader.GetInt32(0);
                                Assert.IsTrue(count == 1);
                            }
                            else
                            {
                                Assert.Fail();
                            }
                        }
                    }
                    connection.Close();
                }

                var datasets = storage.GetDatasetMetadata(randomId);
                Assert.IsTrue(datasets.Count == 1);

                var Id = "new";
                storage.ChangeIdentityId(randomId, Id);
                randomId = Id;

                using (SQLiteConnection connection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", filePath)))
                {
                    connection.Open();

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "select count(*) from datasets where dataset_name = @dataset_name and identity_id = @identity_id ";
                        cmd.Parameters.Add(new SQLiteParameter("@dataset_name", randomDataset));
                        cmd.Parameters.Add(new SQLiteParameter("@identity_id", randomId));
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var count = reader.GetInt32(0);
                                Assert.IsTrue(count == 1);
                            }
                            else
                            {
                                Assert.Fail();
                            }
                        }
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "select count(*) from records where dataset_name = @dataset_name and identity_id = @identity_id ";
                        cmd.Parameters.Add(new SQLiteParameter("@dataset_name", randomDataset));
                        cmd.Parameters.Add(new SQLiteParameter("@identity_id", randomId));
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var count = reader.GetInt32(0);
                                Assert.IsTrue(count == 1);
                            }
                            else
                            {
                                Assert.Fail();
                            }
                        }
                    }
                    connection.Close();
                }

                storage.DeleteDataset(randomId, randomDataset);

                using (SQLiteConnection connection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", filePath)))
                {
                    connection.Open();

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "select last_sync_count from datasets where dataset_name = @dataset_name and identity_id = @identity_id";
                        cmd.Parameters.Add(new SQLiteParameter("@dataset_name", randomDataset));
                        cmd.Parameters.Add(new SQLiteParameter("@identity_id", randomId));
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var count = reader.GetInt32(0);
                                Assert.IsTrue(count == -1);
                            }
                            else
                            {
                                Assert.Fail();
                            }
                        }
                    }
                    connection.Close();

                }

                Assert.IsNotNull(storage.GetDatasetMetadata(randomId)[0]);

            }
        }

    }
}
