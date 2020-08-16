using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Perception.Randomization.Configuration;
using UnityEngine.Perception.Randomization.Parameters;

namespace UnityEngine.Perception.Randomization.Scenarios
{
    /// <summary>
    /// The base class of all scenario classes
    /// </summary>
    public abstract class ScenarioBase : MonoBehaviour
    {
        static ScenarioBase s_ActiveScenario;

        /// <summary>
        /// If true, this scenario will quit the Unity application when it's finished executing
        /// </summary>
        [HideInInspector] public bool quitOnComplete = true;

        /// <summary>
        /// When true, this scenario will deserializes constants from a Json file before it begins executing
        /// </summary>
        [HideInInspector] public bool deserializeOnStart;

        /// <summary>
        /// The name of the Json file this scenario's constants are serialized to/from.
        /// </summary>
        [HideInInspector] public string serializedConstantsFileName = "constants";

        /// <summary>
        /// Returns the active parameter scenario in the scene
        /// </summary>
        public static ScenarioBase ActiveScenario
        {
            get => s_ActiveScenario;
            private set
            {
                if (s_ActiveScenario != null)
                    throw new ScenarioException("There cannot be more than one active ParameterConfiguration");
                s_ActiveScenario = value;
            }
        }

        /// <summary>
        /// Returns the file location of the JSON serialized constants
        /// </summary>
        public string serializedConstantsFilePath =>
            Application.dataPath + "/StreamingAssets/" + serializedConstantsFileName + ".json";

        /// <summary>
        /// The number of frames that have elapsed since the current scenario iteration was Setup
        /// </summary>
        public int currentIterationFrame { get; private set; }

        /// <summary>
        /// The number of frames that have elapsed since the scenario was initialized
        /// </summary>
        public int framesSinceInitialization { get; private set; }

        /// <summary>
        /// The current iteration index of the scenario
        /// </summary>
        public int currentIteration { get; protected set; }

        /// <summary>
        /// Returns whether the current scenario iteration has completed
        /// </summary>
        public abstract bool isIterationComplete { get; }

        /// <summary>
        /// Returns whether the entire scenario has completed
        /// </summary>
        public abstract bool isScenarioComplete { get; }

        /// <summary>
        /// Called before the scenario begins iterating
        /// </summary>
        public virtual void OnInitialize() { }

        /// <summary>
        /// Called when each scenario iteration starts
        /// </summary>
        public virtual void OnIterationSetup() { }

        /// <summary>
        /// Called at the start of every frame
        /// </summary>
        public virtual void OnFrameStart() { }

        /// <summary>
        /// Called right before the scenario iterates
        /// </summary>
        public virtual void OnIterationTeardown() { }

        /// <summary>
        /// Called when the scenario has finished iterating
        /// </summary>
        public virtual void OnComplete() { }

        /// <summary>
        /// Serializes the scenario's constants to a JSON file located at serializedConstantsFilePath
        /// </summary>
        public abstract void Serialize();

        /// <summary>
        /// Deserializes constants saved in a JSON file located at serializedConstantsFilePath
        /// </summary>
        public abstract void Deserialize();

        void OnEnable()
        {
            ActiveScenario = this;
        }

        void OnDisable()
        {
            s_ActiveScenario = null;
        }

        IEnumerator Start()
        {
            if (deserializeOnStart)
                Deserialize();
            foreach (var config in ParameterConfiguration.configurations)
                config.ValidateParameters();
            OnInitialize();
            // TODO: remove this yield when the perception camera no longer skips the first frame of the simulation
            yield return null;
        }

        void Update()
        {
            if (isScenarioComplete)
            {
                OnComplete();
                DatasetCapture.ResetSimulation();
                if (quitOnComplete)
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                return;
            }

            if (isIterationComplete)
            {
                currentIteration++;
                currentIterationFrame = 0;
                OnIterationTeardown();
            }

            if (currentIterationFrame == 0)
            {
                DatasetCapture.StartNewSequence();
                foreach (var config in ParameterConfiguration.configurations)
                    config.ResetParameterStates(currentIteration);
                foreach (var config in ParameterConfiguration.configurations)
                    config.ApplyParameters(currentIteration, ParameterApplicationFrequency.OnIterationSetup);
                OnIterationSetup();
            }

            foreach (var config in ParameterConfiguration.configurations)
                config.ApplyParameters(framesSinceInitialization, ParameterApplicationFrequency.EveryFrame);
            OnFrameStart();
            currentIterationFrame++;
            framesSinceInitialization++;
        }
    }
}
