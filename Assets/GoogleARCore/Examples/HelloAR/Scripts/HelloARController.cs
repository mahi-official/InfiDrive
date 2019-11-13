//-----------------------------------------------------------------------
// <copyright file="HelloARController.cs" company="Google">
//
// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace GoogleARCore.Examples.HelloAR
{
    using System.Collections.Generic;
    using GoogleARCore;
    using GoogleARCore.Examples.Common;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.SceneManagement;
    using Firebase;
    using Firebase.Database;
    using Firebase.Unity.Editor;

#if UNITY_EDITOR
    // Set up touch input propagation while using Instant Preview in the editor.
    using Input = InstantPreviewInput;
#endif

    /// <summary>
    /// Controls the HelloAR example.
    /// </summary>
    public class HelloARController : MonoBehaviour
    {
        /// <summary>
        /// The first-person camera being used to render the passthrough camera image (i.e. AR
        /// background).
        /// </summary>
        public Camera FirstPersonCamera;

        /// <summary>
        /// A prefab for tracking and visualizing detected planes.
        /// </summary>
        public GameObject DetectedPlanePrefab;

        /// <summary>
        /// A prefab to place when a raycast from a user touch hits a feature point.
        /// </summary>
        public GameObject GameObjectPointPrefab;

        /// <summary>
        /// A gameobject parenting UI for displaying the "searching for planes" snackbar.
        /// </summary>
        public GameObject SearchingForPlaneUI;

        /// <summary>
        /// The rotation in degrees need to apply to prefab when it is placed.
        /// </summary>
        private const float k_PrefabRotation = 180.0f;

        /// <summary>
        /// True if the app is in the process of quitting due to an ARCore connection error,
        /// otherwise false.
        /// </summary>
        private bool m_IsQuitting = false;

        /// <summary>
        /// A list to hold all planes ARCore is tracking in the current frame. This object is used across
        /// the application to avoid per-frame allocations.
        /// </summary>
        private List<DetectedPlane> m_AllPlanes = new List<DetectedPlane>();

        public List<GameObject> Points = new List<GameObject>();
        public List<string> Angles = new List<string>();
        public List<string> distance = new List<string>();
        public List<GameObject> measures = new List<GameObject>();
        public GameObject Text;

        public void FirebaseSend()
        {
            Debug.Log("Sending data to Firebase");
            FirebaseApp.DefaultInstance.SetEditorDatabaseUrl("https://major-final.firebaseio.com/");
            DatabaseReference reference = FirebaseDatabase.DefaultInstance.RootReference;
            FirebaseStop();
            int i = 0;
            foreach (string d in distance)
            {
                reference.Child("cmd").Child(i++ + "").SetValueAsync(d);
            }
            i = 0;
            foreach (string a in Angles)
            {
                reference.Child("angle").Child(i++ + "").SetValueAsync(a);
            }
            reference.Child("set").SetValueAsync(distance.Count + "");
            reference.Child("bool").SetValueAsync(true);
        }

        public void FirebaseStop()
        {
            FirebaseApp.DefaultInstance.SetEditorDatabaseUrl("https://major-final.firebaseio.com/");
            DatabaseReference reference = FirebaseDatabase.DefaultInstance.RootReference;
            reference.Child("cmd").SetValueAsync(null);
            reference.Child("angle").SetValueAsync(null);
            reference.Child("set").SetValueAsync("0");
            reference.Child("bool").SetValueAsync(false);
        }

        public void ResetScene()
        {
            FirebaseStop();
            // SceneManager.LoadScene("ARRuler");
            foreach (GameObject go in Points)
            {
                Destroy(go);
            }
            foreach (GameObject po in measures)
            {
                Destroy(po);
            }
            Points.Clear();
            Angles.Clear();
            distance.Clear();
            measures.Clear();
        }

        /// <summary>
        /// The Unity Awake() method.
        /// </summary>
        public void Awake()
        {
            // Enable ARCore to target 60fps camera capture frame rate on supported devices.
            // Note, Application.targetFrameRate is ignored when QualitySettings.vSyncCount != 0.
            Application.targetFrameRate = 60;
        }

        /// <summary>
        /// The Unity Update() method.
        /// </summary>
        public void Update()
        {
            _UpdateApplicationLifecycle();

            // Hide snackbar when currently tracking at least one plane.
            Session.GetTrackables<DetectedPlane>(m_AllPlanes);
            bool showSearchingUI = true;
            for (int i = 0; i < m_AllPlanes.Count; i++)
            {
                if (m_AllPlanes[i].TrackingState == TrackingState.Tracking)
                {
                    showSearchingUI = false;
                    break;
                }
            }

            SearchingForPlaneUI.SetActive(showSearchingUI);

            // If the player has not touched the screen, we are done with this update.
            Touch touch;
            if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began || EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            {
                return;
            }

            // Raycast against the location the player touched to search for planes.
            TrackableHit hit;
            TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
                TrackableHitFlags.FeaturePointWithSurfaceNormal;

            if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit))
            {
                // Use hit pose and camera pose to check if hittest is from the
                // back of the plane, if it is, no need to create the anchor.
                if ((hit.Trackable is DetectedPlane) &&
                    Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
                        hit.Pose.rotation * Vector3.up) < 0)
                {
                    Debug.Log("Hit at back of the current DetectedPlane");
                }
                else
                {   
                    // Instantiate Andy model at the hit pose.
                    var arObject = Instantiate(GameObjectPointPrefab, hit.Pose.position, hit.Pose.rotation);

                    Points.Add(arObject);
                    if (Points.Count >= 2)
                    {
                        arObject.GetComponent<LineRenderer>().positionCount = 2;
                        arObject.GetComponent<LineRenderer>().SetPosition(0, arObject.transform.position);
                        arObject.GetComponent<LineRenderer>().SetPosition(1, Points[Points.Count - 2].transform.position);

                        var temp = Instantiate(Text, (arObject.transform.position + Points[Points.Count - 2].transform.position) / 2, Quaternion.identity);
                        measures.Add(temp);
                        temp.transform.LookAt(arObject.transform.position);
                        temp.transform.localEulerAngles = new Vector3(90, temp.transform.localEulerAngles.y + 90, 2);
                        string f = (Vector3.Distance(arObject.transform.position, Points[Points.Count - 2].transform.position) * 100).ToString("0.00");

                        distance.Add(f);
                        temp.GetComponent<TextMesh>().text = f + " cm";

                        if (Points.Count >= 3)
                        {
                            // Gets a vector that points from the first position to the next.
                            var vec1 = Points[Points.Count - 2].transform.position - Points[Points.Count - 3].transform.position;
                            var vec2 = Points[Points.Count - 1].transform.position - Points[Points.Count - 2].transform.position;
                            string angle = (Vector3.SignedAngle(vec1, vec2, Vector3.up)).ToString("0.00");
                            
                            Angles.Add(angle);
                        }
                    }

                    // Compensate for the hitPose rotation facing away from the raycast (i.e. camera).
                    arObject.transform.Rotate(0, k_PrefabRotation, 0, Space.Self);

                    // Create an anchor to allow ARCore to track the hitpoint as understanding of the physical
                    // world evolves.
                    var anchor = hit.Trackable.CreateAnchor(hit.Pose);

                    // Make Andy model a child of the anchor.
                    arObject.transform.parent = anchor.transform;
                }
            }
        }

        /// <summary>
        /// Check and update the application lifecycle.
        /// </summary>
        private void _UpdateApplicationLifecycle()
        {
            // Exit the app when the 'back' button is pressed.
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            // Only allow the screen to sleep when not tracking.
            if (Session.Status != SessionStatus.Tracking)
            {
                const int lostTrackingSleepTimeout = 15;
                Screen.sleepTimeout = lostTrackingSleepTimeout;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (m_IsQuitting)
            {
                return;
            }

            // Quit if ARCore was unable to connect and give Unity some time for the toast to
            // appear.
            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                _ShowAndroidToastMessage("Camera permission is needed to run this application.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status.IsError())
            {
                _ShowAndroidToastMessage(
                    "ARCore encountered a problem connecting.  Please start the app again.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        /// <summary>
        /// Actually quit the application.
        /// </summary>
        private void _DoQuit()
        {
            Application.Quit();
        }

        /// <summary>
        /// Show an Android toast message.
        /// </summary>
        /// <param name="message">Message string to show in the toast.</param>
        private void _ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject =
                        toastClass.CallStatic<AndroidJavaObject>(
                            "makeText", unityActivity, message, 0);
                    toastObject.Call("show");
                }));
            }
        }
    }
}
