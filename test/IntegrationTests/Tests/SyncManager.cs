
using Amazon.CognitoIdentity;
using Amazon.CognitoSync;
using Amazon.CognitoSync.SyncManager;
using AWSSDK_DotNet.IntegrationTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using Amazon;

namespace AWSSDK_DotNet.IntegrationTests.Tests
{
    [TestClass]
    public class SyncManager : TestBase<AmazonCognitoSyncClient>
    {
        private static RegionEndpoint TEST_REGION = RegionEndpoint.USEast1;

        private static List<string> roleNames = new List<string>();
        private const string policyName = "TestPolicy";

        // Facebook information required to run Facebook tests
        public const string FacebookAppId = "999999999999999";
        public const string FacebookAppSecret = "ffffffffffffffffffffffffffffffff";
        private const string FacebookProvider = "graph.facebook.com";
        FacebookUtilities.FacebookCreateUserResponse facebookUser = null;


        static string poolid = null;
        static string poolName = null;

        [TestCleanup]
        public void Cleanup()
        {
            CognitoIdentity.CleanupIdentityPools();

            CleanupCreatedRoles();

            if (facebookUser != null)
            {
                FacebookUtilities.DeleteFacebookUser(facebookUser);
            }

        }

        [TestMethod]
        [TestCategory("DatasetLocalStorage")]
        public void DatasetLocalStorageTest()
        {
            {
                using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
                {
                    syncManager.WipeData();
                    Dataset d = syncManager.OpenOrCreateDataset("testDataset");
                    d.Put("testKey", "testValue");
                }
            }
            {
                using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
                {
                    Dataset d = syncManager.OpenOrCreateDataset("testDataset");
                    Assert.AreEqual("testValue", d.Get("testkey"));
                }
            }
        }

        // <summary>
        /// Test case: Store a value in a dataset and sync it. Wipe all local data.
        /// After synchronizing the dataset we should have our stored value back.
        /// </summary>
        [TestMethod]
        [TestCategory("DatasetCloudStorage")]
        public void DatasetCloudStorageTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
            {
                syncManager.WipeData();
                Dataset d = syncManager.OpenOrCreateDataset("testDataset2");
                d.Put("key", "he who must not be named");

                d.OnSyncSuccess += delegate(object sender, SyncSuccessEvent e)
                {
                    syncManager.WipeData();
                    string erasedValue = d.Get("key");
                    d.ClearAllDelegates();
                    d.OnSyncSuccess += delegate(object sender2, SyncSuccessEvent e2)
                    {
                        string restoredValues = d.Get("key");
                        Assert.IsNotNull(erasedValue);
                        Assert.IsNotNull(restoredValues);
                        Assert.AreEqual(erasedValue, restoredValues);
                    };
                    d.Synchronize();
                };
                d.Synchronize();
            }
        }

        /// <summary>
        /// Test Case: 
        /// </summary>
        [TestMethod]
        [TestCategory("DatasetMergeTest")]
        public void MergeTest()
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            string uniqueName = ((DateTime.UtcNow - epoch).TotalSeconds).ToString();
            string uniqueName2 = uniqueName + "_";

            UnAuthCredentials.ClearIdentityCache();

            using (CognitoSyncManager sm1 = new CognitoSyncManager(AuthCredentials))
            {
                using (Dataset d = sm1.OpenOrCreateDataset("test"))
                {
                    d.Put(uniqueName, uniqueName);
                    d.OnSyncSuccess += delegate(object s1, SyncSuccessEvent e1)
                    {
                        UnAuthCredentials.ClearIdentityCache();

                        using (CognitoSyncManager sm2 = new CognitoSyncManager(UnAuthCredentials))
                        {
                            using (Dataset d2 = sm2.OpenOrCreateDataset("test"))
                            {
                                d2.Put(uniqueName2, uniqueName2);
                                d2.OnSyncSuccess += delegate(object s2, SyncSuccessEvent e2)
                                {
                                    UnAuthCredentials.ClearIdentityCache();
                                    //now we will use auth credentials.
                                    using (CognitoSyncManager sm3 = new CognitoSyncManager(AuthCredentials))
                                    {
                                        using (Dataset d3 = sm3.OpenOrCreateDataset("test"))
                                        {
                                            d3.OnSyncSuccess += (object sender, SyncSuccessEvent e) =>
                                            {
                                                Assert.Fail();
                                            };
                                            d3.OnSyncConflict += (Dataset dataset, List<SyncConflict> syncConflicts) => { Assert.Fail(); return false; };
                                            d3.OnDatasetDeleted += (Dataset dataset) => { Assert.Fail(); return false; };
                                            d3.OnDatasetMerged += (Dataset ds, List<string> datasetNames) =>
                                            {
                                                d3.ClearAllDelegates();
                                                datasetNames.ForEach((name) =>
                                                {
                                                    Dataset mergedDataset = sm1.OpenOrCreateDataset(name);
                                                    mergedDataset.OnSyncSuccess += (object s3, SyncSuccessEvent e3) =>
                                                    {
                                                        //Check that we have the two datasets to merge
                                                        Assert.AreEqual(d3.Get(uniqueName2), uniqueName2);
                                                        Assert.AreEqual(mergedDataset.Get(uniqueName), uniqueName);

                                                        //We delete the fetched data (ie: we want to keep the local version)
                                                        mergedDataset.Delete();
                                                        mergedDataset.ClearAllDelegates();
                                                        mergedDataset.Synchronize();
                                                    };
                                                    mergedDataset.OnSyncFailure += (object sender, SyncFailureEvent e) => Assert.Fail();
                                                    mergedDataset.OnSyncConflict += (Dataset o, List<SyncConflict> conflicts) => { Assert.Fail(); return false; };
                                                    mergedDataset.OnDatasetDeleted += (Dataset o) => { Assert.Fail(); return false; };
                                                    mergedDataset.OnDatasetMerged += (Dataset o, List<string> names) => { Assert.Fail(); return false; };

                                                    mergedDataset.Synchronize();
                                                });
                                            };
                                        }
                                    }

                                };
                            }
                        }
                    };
                    d.OnSyncFailure += delegate(object s, SyncFailureEvent e) { Assert.Fail(); };
                    d.OnSyncConflict += (Dataset dataset, List<SyncConflict> syncConflicts) => { Assert.Fail(); return false; };
                    d.OnDatasetDeleted += (Dataset dataset) => { Assert.Fail(); return false; };
                    d.OnDatasetMerged += (Dataset dataset, List<string> datasetNames) => { Assert.Fail(); return false; };
                    d.Synchronize();
                }
            }
        }

        /// <summary>
        /// Test case: Check that the dataset metadata is modified appropriately when calling Synchronize.
        /// We test for the dirty bit, the sync count and the last modified timmestamp.
        /// </summary>
        [TestMethod]
        [TestCategory("DatasetMetadataTests")]
        public void MetadataTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
            {
                syncManager.WipeData();
                using (Dataset d = syncManager.OpenOrCreateDataset("testDataset3"))
                {
                    d.Put("testKey3", "the initial value");

                    //Initial properties
                    var records = d.GetAllRecords();
                    Record r = d.GetAllRecords()[records.Count-1];
                    long initialSyncCount = r.SyncCount;
                    bool initialDirty = r.IsModified;
                    DateTime initialDate = r.DeviceLastModifiedDate.Value;

                    d.OnSyncSuccess += delegate(object sender, SyncSuccessEvent e)
                    {
                        //Properties after Synchronize
                        Record r2 = d.GetAllRecords()[records.Count - 1];
                        long synchronizedSyncCount = r2.SyncCount;
                        bool synchronizedDirty = r2.IsModified;
                        DateTime synchronizedDate = r2.DeviceLastModifiedDate.Value;

                        d.Put("testKey3", "a new value");

                        //Properties after changing the content again
                        Record r3 = d.GetAllRecords()[records.Count - 1];
                        long finalSyncCount = r3.SyncCount;
                        bool finalDirty = r3.IsModified;
                        DateTime finalDate = r3.DeviceLastModifiedDate.Value;

                        Assert.IsTrue(initialDirty);
                        Assert.IsTrue(!synchronizedDirty);
                        Assert.IsTrue(finalDirty);

                        Assert.IsTrue(synchronizedSyncCount > initialSyncCount);
                        Assert.IsTrue(synchronizedSyncCount == finalSyncCount);

                        Assert.IsTrue(finalDate > initialDate);
                        Assert.IsTrue(initialDate == synchronizedDate);
                    };
                    d.Synchronize();
                }
            }
        }


        /// <summary>
        /// Test case: Produce a conflict and check that SyncConflict is triggered.
        /// Also check that by returning false in SyncConflict, the Synchronize operation
        /// is aborted and nothing else gets called. 
        /// </summary>
        [TestMethod]
        [TestCategory("DatasetConflictTest")]
        public void ConflictTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
            {
                syncManager.WipeData();
                using (Dataset d = syncManager.OpenOrCreateDataset("testDataset3"))
                {
                    d.Put("testKey3", "the initial value");
                    d.OnSyncSuccess += delegate(object sender, SyncSuccessEvent e)
                    {
                        d.ClearAllDelegates();
                        syncManager.WipeData();
                        using (Dataset d2 = syncManager.OpenOrCreateDataset("testDataset3"))
                        {
                            d2.Put("testKey3", "a different value");

                            d2.OnSyncConflict += delegate(Dataset dataset, List<SyncConflict> conflicts)
                            {
                                return false;
                            };
                            d2.OnSyncSuccess += delegate(object sender4, SyncSuccessEvent e4)
                            {
                                Assert.Fail("Expecting SyncConflict instead of SyncSuccess");
                            };
                            d2.OnSyncFailure += delegate(object sender4, SyncFailureEvent e4)
                            {
                                Assert.Fail("Expecting SyncConflict instead of SyncFailure");
                            };
                            d2.Synchronize();
                        }
                    };
                    d.Synchronize();
                }
            }
        }

        /// <summary>
        /// Test case: Produce a conflict and check that the three ways provided by the SDK
        /// for resolving a conflict (local wins, remote wins, and override) work. We also check
        /// that returning true in SyncConflict allows the Synchronization operationn to continue.
        /// </summary>
        [TestMethod]
        [TestCategory("DatasetResolveConflictTest")]
        public void ResolveConflictTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
            {
                syncManager.WipeData();
                using (Dataset d = syncManager.OpenOrCreateDataset("testDataset4"))
                {
                    d.Put("a", "1");
                    d.Put("b", "2");
                    d.Put("c", "3");
                    d.OnSyncSuccess += delegate(object sender, SyncSuccessEvent e)
                    {
                        d.ClearAllDelegates();
                        syncManager.WipeData();
                        using (Dataset d2 = syncManager.OpenOrCreateDataset("testDataset4"))
                        {
                            d2.Put("a", "10");
                            d2.Put("b", "20");
                            d2.Put("c", "30");

                            bool resolved = false;
                            d2.OnSyncConflict += delegate(Dataset dataset, List<SyncConflict> conflicts)
                            {
                                List<Amazon.CognitoSync.SyncManager.Record> resolvedRecords = new List<Amazon.CognitoSync.SyncManager.Record>();
                                int i = 0;
                                foreach (SyncConflict conflictRecord in conflicts)
                                {
                                    if (i == 0) resolvedRecords.Add(conflictRecord.ResolveWithLocalRecord());
                                    else if (i == 1) resolvedRecords.Add(conflictRecord.ResolveWithValue("42"));
                                    else resolvedRecords.Add(conflictRecord.ResolveWithRemoteRecord());
                                    i++;
                                }
                                dataset.Resolve(resolvedRecords);
                                resolved = true;
                                return true;
                            };
                            d2.OnSyncSuccess += delegate(object sender4, SyncSuccessEvent e4)
                            {
                                if (resolved)
                                {
                                    Assert.AreSame(d2.Get("a"), "10");
                                    Assert.AreSame(d2.Get("b"), "42");
                                    Assert.AreSame(d2.Get("c"), "3");
                                }
                                else
                                {
                                    Assert.Fail("Expecting SyncConflict instead of SyncSuccess");
                                }

                            };
                            d2.OnSyncFailure += delegate(object sender4, SyncFailureEvent e4)
                            {
                                Assert.Fail("Expecting SyncConflict instead of SyncFailure");
                            };
                            d2.Synchronize();
                        }
                    };
                    d.Synchronize();
                }
            }
        }


        private static CognitoAWSCredentials _AuthCredentials;
        private static CognitoAWSCredentials _UnauthCredentials;

        private static CognitoAWSCredentials AuthCredentials
        {
            get
            {
                if (_AuthCredentials != null)
                    return _AuthCredentials;

                if (poolid == null)
                    CognitoIdentity.CreateIdentityPool(out poolid, out poolName);

                //create facebook token
                return null;
            }
        }

        private static CognitoAWSCredentials UnAuthCredentials
        {
            get
            {
                if (_UnauthCredentials != null)
                    return _UnauthCredentials;

                if (poolid == null)
                    CognitoIdentity.CreateIdentityPool(out poolid, out poolName);

                CognitoAWSCredentials credentials = new CognitoAWSCredentials(poolid, TEST_REGION);
                return credentials;
            }
        }


        public static string PrepareRole()
        {
            // Assume role policy which accepts OAuth tokens from Google, Facebook or Cognito, and allows AssumeRoleWithWebIdentity action.
            string assumeRolePolicy = @"{
    ""Version"":""2012-10-17"",
    ""Statement"":[
        {
            ""Effect"":""Allow"",
            ""Principal"":{
                ""Federated"":[""accounts.google.com"",""graph.facebook.com"", ""cognito-identity.amazonaws.com""]
            },
            ""Action"":[""sts:AssumeRoleWithWebIdentity""]
        }
    ]
}";
            // Role policy that allows all operations for a number of services
            var allowPolicy = @"{
    ""Version"" : ""2012-10-17"",
    ""Statement"" : [
        {
            ""Effect"" : ""Allow"",
            ""Action"" : [
                ""ec2:*"",
                ""iam:*"",
                ""cloudwatch:*"",
                ""cognito-identity:*"",
                ""cognito-sync:*"",
                ""s3:*""
            ],
            ""Resource"" : ""*""
        }
    ]
}";
            string roleArn;
            using (var identityClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient())
            {
                string roleName = "NetWebIdentityRole" + new Random().Next();
                var response = identityClient.CreateRole(new Amazon.IdentityManagement.Model.CreateRoleRequest
                {
                    AssumeRolePolicyDocument = assumeRolePolicy,
                    RoleName = roleName
                });

                identityClient.PutRolePolicy(new Amazon.IdentityManagement.Model.PutRolePolicyRequest
                {
                    PolicyDocument = allowPolicy,
                    PolicyName = policyName,
                    RoleName = response.Role.RoleName
                });

                roleArn = response.Role.Arn;
                roleNames.Add(roleName);
            }

            return roleArn;
        }

        public static void CleanupCreatedRoles()
        {
            foreach (var roleName in roleNames)
            {
                DeleteRole(roleName);
            }
            roleNames.Clear();
        }

        private static void DeleteRole(string roleName)
        {
            using (var identityClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient())
            {
                identityClient.DeleteRolePolicy(new Amazon.IdentityManagement.Model.DeleteRolePolicyRequest
                {
                    PolicyName = policyName,
                    RoleName = roleName
                });

                identityClient.DeleteRole(new Amazon.IdentityManagement.Model.DeleteRoleRequest
                {
                    RoleName = roleName
                });
            }
        }
    }
}