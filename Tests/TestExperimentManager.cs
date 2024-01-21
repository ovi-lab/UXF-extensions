using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UXF;

namespace ubco.ovilab.uxf.extensions.tests
{
    public class TestExperimentManager
    {
        const string testSourceDataPath = "Packages/ubco.ovilab.uxf.extensions/Tests/TestDataSource.asset";
        const string testUXFRig = "Packages/ubco.ovilab.uxf.extensions/Tests/[UXF_Rig] Test.prefab";

        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator TestExperimentManagerBaseline()
        {
            GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(testUXFRig));

            GameObject expManagerGO = new GameObject();
            DummyExperimentManager expManager = expManagerGO.AddComponent<DummyExperimentManager>();
            DataSource dataSource = AssetDatabase.LoadAssetAtPath<DataSource>(testSourceDataPath);
            expManager.dataSource = dataSource;

            int test1CalibrationCalled = 0;
            int blockRecievedCalled = 0;

            expManager.AddCalibrationMethod("test1", (_) => test1CalibrationCalled++);
            expManager.onBlockRecieved.AddListener((_) => blockRecievedCalled++);

            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return null;

            // Setting params
            expManager.SessionBeginParams("test", 123, null, null);
            Assert.AreEqual(expManager.sessionNumber, 123);
            yield return null;

            // Getting data
            Assert.AreEqual(blockRecievedCalled, 0);
            expManager.MoveToNextState();
            Assert.AreEqual(blockRecievedCalled, 1);
            yield return null;

            // Start session
            Assert.AreEqual(expManager.sessionBeginCalled, 0);
            expManager.MoveToNextState();
            Assert.AreEqual(expManager.sessionBeginCalled, 1);
            yield return null;

            // Calibration call
            Assert.AreEqual(test1CalibrationCalled, 0);
            expManager.MoveToNextState();
            Assert.AreEqual(test1CalibrationCalled, 1);
            yield return null;

            // Shouldn't start block as calibration is still happening (i.e. CalibrationComplete not called)
            Assert.AreEqual(expManager.configureBlockCalled, 0);
            expManager.MoveToNextState();
            Assert.AreEqual(expManager.configureBlockCalled, 0);
            yield return null;

            // Ending calibration
            expManager.CalibrationComplete(null);
            yield return null;

            // Starting block/trial
            Assert.AreEqual(expManager.blockBegingCalled, 0);
            Assert.AreEqual(expManager.trialBeginCalled, 0);
            expManager.MoveToNextState();
            Assert.AreEqual(expManager.configureBlockCalled, 1);
            Assert.AreEqual(expManager.blockBegingCalled, 1);
            Assert.AreEqual(expManager.trialBeginCalled, 1);
            yield return null;

            // Endin trial (and block, as only on trial)
            Assert.AreEqual(blockRecievedCalled, 1);
            Assert.AreEqual(expManager.trialEndCalled, 0);
            Assert.AreEqual(expManager.blockEndCalled, 0);
            Session.instance.EndCurrentTrial();
            Assert.AreEqual(blockRecievedCalled, 2);
            Assert.AreEqual(expManager.trialEndCalled, 1);
            Assert.AreEqual(expManager.blockEndCalled, 1);
            yield return null;

            // No calibration, should start next trial
            Assert.AreEqual(expManager.configureBlockCalled, 1);
            Assert.AreEqual(expManager.blockBegingCalled, 1);
            Assert.AreEqual(expManager.trialBeginCalled, 1);
            expManager.MoveToNextState(); // Start calibration, but no calibration is happening
            expManager.MoveToNextState(); // Start trial
            Assert.AreEqual(expManager.configureBlockCalled, 2);
            Assert.AreEqual(expManager.blockBegingCalled, 2);
            Assert.AreEqual(expManager.trialBeginCalled, 2);
            yield return null;

            // cancelling
            Assert.AreEqual(expManager.cancelCalled, 0);
            Assert.AreEqual(expManager.trialEndCalled, 1);
            Assert.AreEqual(expManager.blockEndCalled, 1);
            expManager.MoveToNextState();
            Assert.AreEqual(expManager.trialEndCalled, 2);
            Assert.AreEqual(expManager.blockEndCalled, 2);
            Assert.AreEqual(expManager.cancelCalled, 1);
            yield return null;

            // restarting block
            Assert.AreEqual(expManager.configureBlockCalled, 2);
            Assert.AreEqual(expManager.blockBegingCalled, 2);
            Assert.AreEqual(expManager.trialBeginCalled, 2);
            expManager.MoveToNextState(); // Start calibration, but no calibration is happening
            expManager.MoveToNextState(); // Start trial
            Assert.AreEqual(expManager.configureBlockCalled, 3);
            Assert.AreEqual(expManager.blockBegingCalled, 3);
            Assert.AreEqual(expManager.trialBeginCalled, 3);
            yield return null;

            // this should close the session
            Assert.AreEqual(blockRecievedCalled, 3);
            Assert.AreEqual(expManager.trialEndCalled, 2);
            Assert.AreEqual(expManager.blockEndCalled, 2);
            Assert.AreEqual(expManager.sessionEndCalled, 0);
            Session.instance.EndCurrentTrial();
            Assert.AreEqual(blockRecievedCalled, 3);
            Assert.AreEqual(expManager.trialEndCalled, 3);
            Assert.AreEqual(expManager.blockEndCalled, 3);
            Assert.AreEqual(expManager.sessionEndCalled, 1);
        }
    }

    public class DummyExperimentManager : ExperimentManager<BlockData>
    {
        public int configureBlockCalled = 0,
            blockBegingCalled = 0,
            blockEndCalled = 0,
            sessionBeginCalled = 0,
            sessionEndCalled = 0,
            trialBeginCalled = 0,
            trialEndCalled = 0,
            cancelCalled = 0 ;

        protected override void ConfigureBlock(BlockData el, Block block, bool lastBlockCancelled)
        {
            configureBlockCalled++;
            block.CreateTrial();
        }

        protected override void OnBlockBegin(Block block)
        {
            blockBegingCalled++;
        }

        protected override void OnBlockEnd(Block block)
        {
            blockEndCalled++;
            if (block.settings.GetBool("canceled"))
            {
                cancelCalled++;
            }
        }

        protected override void OnSessionBegin(Session session)
        {
            sessionBeginCalled++;
        }

        protected override void OnSessionEnd(Session session)
        {
            sessionEndCalled++;
        }

        protected override void OnTrialBegin(Trial trial)
        {
            trialBeginCalled++;
        }

        protected override void OnTrialEnd(Trial trial)
        {
            trialEndCalled++;
        }
    }
}
