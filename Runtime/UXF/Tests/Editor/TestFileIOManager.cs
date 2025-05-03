﻿using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;
using System.IO;

namespace UXF.Tests
{
    public class TestFileSaver
    {
        string experiment = "fileSaver_test";
        string ppid = "test_ppid";
        int sessionNum = 1;
        FileSaver fileSaver;
        Session session;

        [SetUp]
        public void SetUp()
        {
            var gameObject = new GameObject();
            fileSaver = gameObject.AddComponent<FileSaver>();
            fileSaver.verboseDebug = true;
            if (Session.instance != null) GameObject.DestroyImmediate(Session.instance.gameObject);
            session = gameObject.AddComponent<Session>();
            session.experimentName = "test_experiment";
            session.ppid = "P001";
            session.number = 1;
            fileSaver.Initialise(session);
        }


        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(fileSaver.gameObject);
        }


        [Test]
        public void WriteManyFiles()
        {
            fileSaver.StoragePath = "example_output";
            fileSaver.SetUp();

            // generate a large dictionary
            var dict = new Dictionary<string, object>();

            var largeArray = new string[100];
            string largeString = new string('*', 50000);

            // write lots and lots of JSON files
            int n = 100;
            string[] fpaths = new string[n];
            for (int i = 0; i < n; i++)
            {
                string fileName = string.Format("{0}", i);
                Debug.LogFormat("Queueing {0}", fileName);
                string fpath = fileSaver.HandleText(largeString, experiment, ppid, sessionNum, fileName,
                    UXFDataType.OtherSessionData);
                fpaths[i] = fpath;
            }

            Debug.Log("###########################################");
            Debug.Log("############## CLEANING UP ################");
            Debug.Log("###########################################");
            fileSaver.CleanUp();

            Assert.Throws<System.InvalidOperationException>(() =>
            {
                fileSaver.HandleText(largeString, experiment, ppid, sessionNum, "0", UXFDataType.OtherSessionData);
            });

            // cleanup files
            foreach (var fpath in fpaths)
            {
                System.IO.File.Delete(Path.Combine(fileSaver.StoragePath, fpath));
            }
        }


        [Test]
        public void EarlyExit()
        {
            fileSaver.StoragePath = "example_output";
            fileSaver.SetUp();
            fileSaver.CleanUp();

            Assert.Throws<System.InvalidOperationException>(
                () => { fileSaver.ManageInWorker(() => Debug.Log("Code enqueued after FileSaver ended")); }
            );

            fileSaver.SetUp();
            fileSaver.ManageInWorker(() => Debug.Log("Code enqueued after FileSaver re-opened"));
            fileSaver.CleanUp();
        }

        [Test]
        public void AbsolutePath()
        {
            if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows)
            {
                fileSaver.StoragePath = "C:/example_output";
            }
            else
            {
                fileSaver.StoragePath = "/tmp/example_output";
            }
            fileSaver.SetUp();

            string outString =
                fileSaver.HandleText("abc", experiment, ppid, sessionNum, "test", UXFDataType.OtherSessionData);

            Assert.AreEqual(outString, @"fileSaver_test/test_ppid/S001/other/test.txt");

            fileSaver.CleanUp();
        }

        [Test]
        public void PersistentDataPath()
        {
            fileSaver.dataSaveLocation = DataSaveLocation.PersistentDataPath;
            fileSaver.SetUp();
            Assert.AreEqual(Application.persistentDataPath, fileSaver.StoragePath);

            string dataOutput = "abc";
            fileSaver.HandleText(dataOutput, experiment, ppid, sessionNum, "test", UXFDataType.OtherSessionData);
            fileSaver.CleanUp();
            string outFile = Path.Combine(Application.persistentDataPath,
                @"fileSaver_test/test_ppid/S001/other/test.txt");

            string readData = File.ReadAllText(outFile);
            Assert.AreEqual(dataOutput, readData);

            if (File.Exists(outFile)) File.Delete(outFile);
        }

        [Test]
        public void FileSaverRelPath()
        {
            Assert.AreEqual(
                FileSaver.GetRelativePath("C:\\base", "C:\\base\\123"),
                "123"
            );

            Assert.AreEqual(
                FileSaver.GetRelativePath("base", "base\\123"),
                "123"
            );

            Assert.AreEqual(
                FileSaver.GetRelativePath("base/", "base\\123"),
                SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows ? "123" : "../123"
            );

            Assert.AreEqual(
                FileSaver.GetRelativePath("C:/base/", "C:/base\\123"),
                SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows ? "123" : "../123" 
            );
        }

        [Test]
        public void TestBackupSession()
        {
            fileSaver.StoragePath = "test_output";
            fileSaver.backupSessionIfExists = true;
            if (Directory.Exists(fileSaver.StoragePath))
            {
                Directory.Delete(fileSaver.StoragePath, true);
            }

            fileSaver.SetUp();

            string fileName = "testMoveToBackup";
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);

            fileSaver.CleanUp();
            System.Threading.Thread.Sleep(500);

            fileSaver.SetUp();

            fileName = "testMoveToBackup";
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);

            fileSaver.CleanUp();

            string testFilesDirectory = fileSaver.GetSessionPath(session.experimentName, session.ppid, session.number);

            string[] directories = Directory.GetDirectories(Directory.GetParent(testFilesDirectory).ToString(), $"{FileSaver.SessionNumToName(1)}*", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(directories.Length, 2);

            Directory.Delete(fileSaver.StoragePath, true);
        }

        [Test]
        public void TestSessionOverwrite()
        {
            fileSaver.StoragePath = "test_output";
            fileSaver.backupSessionIfExists = false;
            if (Directory.Exists(fileSaver.StoragePath))
            {
                Directory.Delete(fileSaver.StoragePath, true);
            }

            fileSaver.SetUp();

            string fileName = "testMoveToBackup";
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);

            fileSaver.CleanUp();
            System.Threading.Thread.Sleep(500);

            fileSaver.SetUp();

            fileName = "testMoveToBackup";
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);
            fileSaver.HandleText("", session.experimentName, session.ppid, session.number, fileName, UXFDataType.TrialResults);

            fileSaver.CleanUp();

            string testFilesDirectory = fileSaver.GetSessionPath(session.experimentName, session.ppid, session.number);

            string[] directories = Directory.GetDirectories(Directory.GetParent(testFilesDirectory).ToString(), $"{FileSaver.SessionNumToName(1)}*", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(directories.Length, 1);

            Directory.Delete(fileSaver.StoragePath, true);
        }
    }

}
