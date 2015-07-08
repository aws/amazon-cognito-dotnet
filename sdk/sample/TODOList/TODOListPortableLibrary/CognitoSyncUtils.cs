﻿using Amazon.CognitoIdentity;
using Amazon.Runtime;
using System;
using System.Collections.Generic;
using Amazon.CognitoSync.SyncManager;
using Amazon.CognitoSync;
using Newtonsoft.Json;

namespace TODOListPortableLibrary
{
    public class CognitoSyncUtils
    {
        private static CognitoAWSCredentials _credentials;

        public static CognitoAWSCredentials Credentials
        {
            get
            {
                if (_credentials == null)
                    _credentials = new CognitoAWSCredentials(Constants.IdentityPoolId, Constants.CognitoIdentityRegion);

                return _credentials;
            }
        }

        private static CognitoSyncManager _syncManager;
        public static CognitoSyncManager SyncManagerInstance
        {
            get
            {
                if (_syncManager == null)
                    _syncManager = new CognitoSyncManager(Credentials, new AmazonCognitoSyncConfig { RegionEndpoint = Constants.CognitoSyncRegion });

                return _syncManager;
            }
        }

        private const string TASK_DATASET = "TASK_DATASET";

        public void SaveTask(Task task)
        {
            Dataset dataset = SyncManagerInstance.OpenOrCreateDataset(TASK_DATASET);
            var taskJson = JsonConvert.SerializeObject(task);
            dataset.Put(task.Id, taskJson);
        }

        public void DeleteTask(string id)
        {
            Dataset dataset = SyncManagerInstance.OpenOrCreateDataset(TASK_DATASET);

            dataset.Remove(id);

        }

        public void SaveTask(string title, string description, bool completed)
        {
            SaveTask(new Task
            {
                Id = Guid.NewGuid().ToString(),
                Title = title,
                Completed = completed,
                Description = description
            });
        }

        public List<Task> GetTasks()
        {
            List<Task> tasks = new List<Task>();
            Dataset dataset = SyncManagerInstance.OpenOrCreateDataset(TASK_DATASET);
            var records = dataset.GetAllRecords();
            foreach (var record in records)
            {
                tasks.Add(JsonConvert.DeserializeObject<Task>(record.Value));
            }

            return tasks;
        }

        public Task GetTask(string id)
        {
            Task taskObject = null;
            Dataset dataset = SyncManagerInstance.OpenOrCreateDataset(TASK_DATASET);
            var record = dataset.GetRecord(id);
            if (record != null && record.Value != null)
            {
                taskObject = JsonConvert.DeserializeObject<Task>(record.Value);
            }
            return taskObject;
        }

        public async System.Threading.Tasks.Task Synchronize(Action action)
        {
            Dataset dataset = SyncManagerInstance.OpenOrCreateDataset(TASK_DATASET);
            dataset.OnSyncSuccess += (object sender, SyncSuccessEvent e) =>
            {
                action();
            };
            dataset.OnDatasetDeleted = delegate
            {
                //basically use what ever we got in remote
                return true;
            };
            dataset.OnSyncConflict = delegate(Dataset ds, List<SyncConflict> conflicts)
            {
                //trust the remote
                List<Record> resolved = new List<Record>();
                foreach(SyncConflict sc in conflicts)
                {
                    resolved.Add(sc.ResolveWithRemoteRecord());
                }
                dataset.Resolve(resolved);
                return true;
            };
            await dataset.SynchronizeAsync();
        }

    }
}