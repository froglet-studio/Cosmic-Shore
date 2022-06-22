using StarWriter.Utility.Singleton;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarWriter.Core.Tutorial
{
    public class TutorialManager : Singleton<TutorialManager>
    {
        public List<GameObject> tutorialPanels;

        public List<TutorialStage> tutorialStages; // SO Assests

        [SerializeField]
        TutorialMuton muton;

        [SerializeField]
        TutorialJailBlockWall jailBlockWall;

        [SerializeField]
        private IntensityBar intensityBar;

        [SerializeField]
        private GameObject player;

        [SerializeField]
        private IntensitySystem intensitySystem;

        [SerializeField]
        private TextMeshProUGUI dialogueText;
        public Sprite dialogueBox;

        private TrailSpawner trailSpawner;
        private int index = 0;

        private void OnEnable()
        {
            IntensitySystem.zeroIntensity += IntensityBarDrained;
            TutorialMuton.onMutonCollision += MutonCollision;
            TutorialJailBlockWall.onJailBlockCollision += JailBlockCollision;
        }

        private void OnDisable()
        {
            IntensitySystem.zeroIntensity -= IntensityBarDrained;
            TutorialMuton.onMutonCollision -= MutonCollision;
            TutorialJailBlockWall.onJailBlockCollision -= JailBlockCollision;
        }

        void Start()
        {
            trailSpawner = player.GetComponent<TrailSpawner>();

            // Disable stuff to start
            muton.gameObject.SetActive(false);
            jailBlockWall.gameObject.SetActive(false);
            intensityBar.gameObject.SetActive(false);
            intensitySystem.enabled = false;
            trailSpawner.enabled = false;
            GameSetting.Instance.TurnGyroOFF();
                

            InitializeTutorialStages();

            BeginStage();
        }

        IEnumerator EndStageTimerCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            EndStage();
        }

        private void BeginStage(float delay = 0)
        {
            StopAllCoroutines();
            StartCoroutine(BeginStageCoroutine(delay));
        }

        // Optionally wait for delay (if previous level fail text is displayed)
        // Setup stage elements
        // Start Timeout Timer
        // Begin Stage
        // Show prompt
        IEnumerator BeginStageCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            TutorialStage stage = tutorialStages[index];
            
            muton.gameObject.SetActive(stage.HasMuton);
            jailBlockWall.gameObject.SetActive(stage.UsesJailBlockWall);
            intensityBar.gameObject.SetActive(stage.UsesFuelBar);
            intensitySystem.enabled = stage.UsesFuelBar;
            trailSpawner.enabled = stage.UsesTrails;
            if (stage.UsesGyro) GameSetting.Instance.TurnGyroON(); else GameSetting.Instance.TurnGyroOFF();

            if (stage.HasMuton)
                muton.MoveMuton(player.transform, stage.MutonSpawnOffset);

            if (stage.UsesJailBlockWall)
                jailBlockWall.MoveJailBlockWall(player.transform, stage.JailBlockSpawnOffset);

            if (stage.PlayTime > 0)
                StartCoroutine(EndStageTimerCoroutine(stage.PlayTime));

            if (stage.UsesFuelBar)
                IntensitySystem.ResetIntensity();
            
                

            stage.Begin();
            UpdateDialogueTextBox();
        }

        // Decrement Retry Counter
        // Show Retry Text
        // If out of retries, end stage
        // Else Reset Stage Components
        private void RetryStage()
        {
            TutorialStage stage = tutorialStages[index];
            stage.Retry();

            Debug.Log($"TutorialManager.RetryStage - Index: {index}, Has Remaining Attempts: {stage.HasRemainingAttempts}");

            if (stage.HasRemainingAttempts)
            {
                if (stage.RetryLine != null)
                {
                    // Show Retry
                    dialogueText.text = stage.RetryLine.Text;
                    StartCoroutine(DelayFadeOfTextBox(stage.RetryLineDisplayTime));
                }
                stage.Retry();
            }
            else
            {
                EndStage(false);
            }
        }

        // End current stage
        // Increment Index
        // If not success show failure text ... and wait for it to time out before going to the next one
        // If was last stage, complete tutorial
        // Else Begin next stage
        private void EndStage(bool success = true)
        {
            Debug.Log($"TutorialManager.EndStage - Index: {index}, Success: {success}");

            TutorialStage stage = tutorialStages[index++];
            stage.End();

            if (index >= tutorialStages.Count)
            {
                CompleteTutorial();
            }
            else
            {
                // If we succeeded, or the stage doesn't have a failure prompt, immediately go to the next stage
                // Otherwise, show the error prompt first
                if (success || stage.FailureLine == null)
                {
                    BeginStage();
                }
                else
                {
                    // Show Failure prompt
                    dialogueText.text = stage.FailureLine.Text;
                    StartCoroutine(DelayFadeOfTextBox(stage.FailLineDisplayTime));
                    BeginStage(stage.FailLineDisplayTime);
                }
            }
        }

        /// <summary>
        /// Adds Panels to tutorialStages
        /// </summary>
        private void InitializeTutorialStages()
        {
            int idx = 0;
            foreach (TutorialStage stage in tutorialStages)
            {
                tutorialStages[idx].UiPanel = tutorialPanels[idx];
                idx++;
            }
        }

        private void Update()
        {   
            var stage = tutorialStages[index];
            
            // Reset Muton if it's too far away
            if (stage.HasMuton)
            {
                // TODO: problem here if respawn distance is too low, it will just keep moving the muton further away
                //if (stage.RespawnDistance >= (Vector3.Distance(player.transform.position, muton.transform.position)))
                //{
                //    muton.MoveMuton(player.transform, stage.MutonSpawnOffset);
                //}
            }

            // Reset Jail Block if it's too far away
            //if (stage.UsesJailBlockWall)
            //{
            //    if (stage.RespawnDistance >= Vector3.Distance(player.transform.position, jailBlockWall.transform.position))
            //    {
            //        jailBlockWall.MoveJailBlockWall(player.transform, stage.JailBlockSpawnOffset);
            //    }
            //}
        }

        IEnumerator DelayFadeOfTextBox(float time)
        {
            yield return new WaitForSeconds(time);
            dialogueText.enabled = false;
        }

        /// <summary>
        /// Updates the text in the Dialogue Text Box
        /// </summary>
        private void UpdateDialogueTextBox()
        {
            dialogueText.enabled = true;
            dialogueText.text = tutorialStages[index].PromptLine.Text;
            StartCoroutine(DelayFadeOfTextBox(tutorialStages[index].PromptLineDisplayTime));
        }

        /// <summary>
        /// Tells GameSettings and GameManager that Tutorial has been completed
        /// </summary>
        public void CompleteTutorial()
        {
            GameSetting.Instance.TutorialHasBeenCompleted = true;
            SceneManager.LoadScene(0);
        }

        public void JailBlockCollision()
        {
            RetryStage();
        }

        private void IntensityBarDrained()
        {
            IntensitySystem.ResetIntensity();
            RetryStage();
        }

        public void MutonCollision()
        {
            EndStage();
        }
    }
}