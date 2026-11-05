using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class LoadingScreenManager : MonoBehaviour
{
    public VideoPlayer loadingVideoPlayer;
    public string targetSceneName = "SampleScene"; // Set to your main game scene name

    void Start()
    {
        if (loadingVideoPlayer != null)
        {
            // Trigger scene loading once the video finishes playing
            loadingVideoPlayer.loopPointReached += OnVideoEnd;
        }
        else
        {
            StartCoroutine(LoadSceneAsync());
        }
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        StartCoroutine(LoadSceneAsync());
    }

    IEnumerator LoadSceneAsync()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName);

        // Prevent immediate scene activation if you want the video to finish completely first
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            // When progress reaches 0.9, the scene is fully loaded in memory
            if (operation.progress >= 0.9f)
            {
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}