using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PCLStorage;
using CommonTests.Framework;
using System.Net;
using System.Threading;
using System.IO;
using Amazon.CognitoSync;
using Amazon.CognitoSync.SyncManager.Internal;
using SQLitePCL;
using Amazon.Util.Internal.PlatformServices;

namespace CommonTests.IntegrationTests.SyncManager
{
    [TestFixture]
    public class SQLiteLocalStorageTests
    {
        static string DB_FILE_NAME = "aws_cognito_sync.db";

        [TearDown]
        public void Cleaup()
        {
            string dbPath = Path.Combine(PCLStorage.FileSystem.Current.LocalStorage.Path, DB_FILE_NAME);

            //drop all the tables from the db
            using (SQLiteConnection connection = new SQLiteConnection(dbPath))
            {
                using (var sqliteStatement = connection.Prepare("DROP TABLE IF EXISTS records"))
                {
                    var result = sqliteStatement.Step();
                }

                using (var sqliteStatement = connection.Prepare("DROP TABLE IF EXISTS datasets"))
                {
                    var result = sqliteStatement.Step();
                }
            }
        }


        [Test]
        public void SqliteInitializationTest()
        {
            string dbPath = Path.Combine(PCLStorage.FileSystem.Current.LocalStorage.Path, DB_FILE_NAME);

            using (SQLiteLocalStorage storage = new SQLiteLocalStorage())
            { }

            using (SQLiteConnection connection = new SQLiteConnection(dbPath))
            {

                var query = "SELECT name FROM sqlite_master WHERE type='table'";
                var tableName = new List<string>();

                using (var sqliteStatement = connection.Prepare(query))
                {
                    while(sqliteStatement.Step() == SQLiteResult.ROW)
                    {
                        tableName.Add(sqliteStatement.GetText(0));
                    }
                }

                Assert.IsTrue(tableName.Count == 2);
                Assert.IsTrue(tableName.Contains("datasets"));
                Assert.IsTrue(tableName.Contains("records")); 
            }
        }

        [Test]
        public void SQliteDatasetsTests()
        {
            string dbPath = Path.Combine(PCLStorage.FileSystem.Current.LocalStorage.Path, DB_FILE_NAME);

            string randomId = "old";
            string randomDataset = Guid.NewGuid().ToString();
            using (SQLiteLocalStorage storage = new SQLiteLocalStorage())
            {
                storage.WipeData();
                storage.CreateDataset(randomId, randomDataset);
                storage.PutValue(randomId, randomDataset, "Voldemort", "He who must not be named");

                using (SQLiteConnection connection = new SQLiteConnection(dbPath))
                {
                    string query = "select count(*) from datasets where dataset_name = @dataset_name and identity_id = @identity_id ";
                    using (var sqliteStatement = connection.Prepare(query))
                    {
                        BindData(sqliteStatement, randomDataset, randomId);
                        if(sqliteStatement.Step()==SQLiteResult.ROW)
                        {
                            var count = sqliteStatement.GetInteger(0);
                            Assert.IsTrue(count == 1);
                        }
                        else
                        {
                            Assert.Fail();
                        }

                    }

                    query = "select count(*) from records where dataset_name = @dataset_name and identity_id = @identity_id ";

                    using( var sqliteStatement = connection.Prepare(query))
                    {
                        BindData(sqliteStatement, randomDataset, randomId);
                        if (sqliteStatement.Step() == SQLiteResult.ROW)
                        {
                            var count = sqliteStatement.GetInteger(0);
                            Assert.IsTrue(count == 1);
                        }
                        else
                        {
                            Assert.Fail();
                        }
                    }
                }

                var datasets = storage.GetDatasetMetadata(randomId);
                Assert.IsTrue(datasets.Count == 1);

                var Id = "new";
                storage.ChangeIdentityId(randomId, Id);
                randomId = Id;

                using (SQLiteConnection connection = new SQLiteConnection(dbPath))
                {
                    var query = "select count(*) from datasets where dataset_name = @dataset_name and identity_id = @identity_id ";
                    using (var sqliteStatement = connection.Prepare(query))
                    {
                        BindData(sqliteStatement, randomDataset, randomId);
                        if (sqliteStatement.Step() == SQLiteResult.ROW)
                        {
                            var count = sqliteStatement.GetInteger(0);
                            Assert.IsTrue(count == 1);
                        }
                        else
                        {
                            Assert.Fail();
                        }
                    }


                    query = "select count(*) from records where dataset_name = @dataset_name and identity_id = @identity_id ";
                    using (var sqliteStatement = connection.Prepare(query))
                    {
                        BindData(sqliteStatement, randomDataset, randomId);
                        if (sqliteStatement.Step() == SQLiteResult.ROW)
                        {
                            var count = sqliteStatement.GetInteger(0);
                            Assert.IsTrue(count == 1);
                        }
                        else
                        {
                            Assert.Fail();
                        }
                    }
                }

                storage.DeleteDataset(randomId, randomDataset);

                using (SQLiteConnection connection = new SQLiteConnection(dbPath))
                {
                    var query = "select last_sync_count from datasets where dataset_name = @dataset_name and identity_id = @identity_id";

                    using (var sqliteStatement = connection.Prepare(query))
                    {
                        BindData(sqliteStatement, randomDataset, randomId);
                        if (sqliteStatement.Step() == SQLiteResult.ROW)
                        {
                            var count = sqliteStatement.GetInteger(0);
                            Assert.IsTrue(count == -1);
                        }
                        else
                        {
                            Assert.Fail();
                        }
                    }

                }

                Assert.IsNotNull(storage.GetDatasetMetadata(randomId)[0]);

            }
        }


        private static void BindData(ISQLiteStatement statement, params object[] parameters)
        {
            if (parameters != null)
            {
                for (int i = 1; i <= parameters.Length; i++)
                {
                    object o = parameters[i - 1];

                    var dt = o as DateTime?;
                    if (dt.HasValue)
                    {
                        string ticks = dt.Value.Ticks.ToString();
                        statement.Bind(i, ticks);
                    }
                    else
                    {
                        statement.Bind(i, o);
                    }
                }
            }
        }

    }
}