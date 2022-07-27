﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Runtime.InteropServices;
using System.IO;
using BeatSaberDMX;
using UnityEngine.SceneManagement;

#if UNITY_2017_2_OR_NEWER
using UnityEngine.XR;
#endif

namespace MikanXR.SDK.Unity
{
    using MikanSpatialAnchorID = System.Int32;

    [System.Serializable]
    public class MikanPoseUpdateEvent : UnityEvent<MikanMatrix4f>
    {
    }

    [HelpURL("https://github.com/MikanXR/MikanXR_Unity")]
    [AddComponentMenu("MikanXR/Mikan")]
    public class MikanClient : MonoBehaviour
    {
        private static String GameSceneName = "StandardGameplay";
        private static String MenuSceneName = "MainMenu";
        private List<string> _loadedSceneNames = new List<string>();

        private static MikanClient _instance = null;

        private MikanClientInfo _clientInfo;
        private MikanRenderTargetMemory _renderTargetMemory;
        private MikanStencilQuad _stencilQuad;
        private Matrix4x4 _originSpatialAnchorXform = Matrix4x4.identity;
        private RenderTexture _renderTexture;
        private AsyncGPUReadbackRequest _readbackRequest = new AsyncGPUReadbackRequest();

        private bool _apiInitialized = false;
        private float _mikanReconnectTimeout = 0.0f;
        private ulong _lastReceivedVideoSourceFrame = 0;
        private ulong _lastRenderedFrame = 0;
        private int _loadedSceneCounter = 0;

        public UnityEvent _connectEvent = new UnityEvent();
        public UnityEvent ConnectEvent
        {
            get { return _connectEvent; } 
        }

        public UnityEvent _disconnectEvent = new UnityEvent();
        public UnityEvent DisconnectEvent
        {
            get { return _disconnectEvent; }
        }

        private Dictionary<MikanSpatialAnchorID, MikanPoseUpdateEvent> _anchorPoseEvents = new Dictionary<MikanSpatialAnchorID, MikanPoseUpdateEvent>();

        public Color BackgroundColorKey = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        public static MikanClient Instance
        {
            get
            {
                return _instance;
            }
        }

        private Camera _MRCamera = null;

        private void Awake()
        {
            // For this particular MonoBehaviour, we only want one instance to exist at any time, so store a reference to it in a static property
            //   and destroy any that are created while one already exists.
            if (_instance != null)
            {
                Plugin.Log?.Warn($"Mikan: Instance of {GetType().Name} already exists, destroying.");
                GameObject.DestroyImmediate(this);
                return;
            }

            GameObject.DontDestroyOnLoad(this); // Don't destroy this object on scene changes
            _instance = this;
            Plugin.Log?.Debug($"Mikan: {name}: Awake()");

            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
        }

        void OnEnable()
        {
            _apiInitialized = false;

            MikanClientGraphicsAPI graphicsAPI = MikanClientGraphicsAPI.UNKNOWN;
            switch(SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    graphicsAPI = MikanClientGraphicsAPI.Direct3D11;
                    break;
                case GraphicsDeviceType.OpenGLCore:
                case GraphicsDeviceType.OpenGLES2:
                case GraphicsDeviceType.OpenGLES3:
                    graphicsAPI = MikanClientGraphicsAPI.OpenGL;
                    break;
            }

            _clientInfo = new MikanClientInfo()
            {
                supportedFeatures = MikanClientFeatures.RenderTarget_RGBA32,
                engineName = "unity",
                engineVersion = Application.unityVersion,
                applicationName = Application.productName,
                applicationVersion = Application.version,
#if UNITY_2017_2_OR_NEWER
                xrDeviceName = XRSettings.loadedDeviceName,
#endif
                graphicsAPI = graphicsAPI,
                mikanSdkVersion = SDKConstants.SDK_VERSION,
            };

            MikanResult result= MikanClientAPI.Mikan_Initialize(MikanLogLevel.Info, "UnityClient.log");
            if (result == MikanResult.Success)
            {
                _apiInitialized = true;
            }
        }

        void OnDisable()
        {
            if (_apiInitialized)
            {
                if (!_readbackRequest.done)
                {
                    _readbackRequest.WaitForCompletion();
                }

                freeFrameBuffer();
                MikanClientAPI.Mikan_Shutdown();
                _apiInitialized = false;
            }
        }

        private void SceneManager_sceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
        {
            Plugin.Log?.Info($"Mikan: New Scene Loaded: {loadedScene.name}");

            if (_loadedSceneNames.Count == 0)
            {
                SpawnMikanCamera();
            }

            _loadedSceneNames.Add(loadedScene.name);
            UpdateCameraAttachment();
        }

        private void SceneManager_sceneUnloaded(Scene unloadedScene)
        {
            Plugin.Log?.Info($"Mikan: Unloading scene {unloadedScene.name}");

            _loadedSceneNames.Remove(unloadedScene.name);
            if (_loadedSceneNames.Count == 0)
            {
                DespawnMikanCamera();
            }
            else
            {
                UpdateCameraAttachment();
            }
        }

        private void UpdateCameraAttachment()
        {
            if (_MRCamera == null)
            {
                return;
            }

            // Find the camera origin based on the scene taht
            Transform CameraOrigin = null;
            
            if (_loadedSceneNames.Contains(GameSceneName))
            {
                Scene gameScene= SceneManager.GetSceneByName(GameSceneName);

                //Plugin.Log?.Warn("[Scene Game Objects]");
                //PluginUtils.PrintObjectTreeInScene(gameScene);

                GameObject localPlayerGameCore = PluginUtils.FindGameObjectRecursiveInScene(gameScene, "LocalPlayerGameCore");
                //PluginUtils.PrintComponents(localPlayerGameCore);
                if (localPlayerGameCore != null)
                {
                    CameraOrigin = localPlayerGameCore.transform.Find("Origin");
                    //PluginUtils.PrintComponents(GameOrigin?.gameObject);
                    if (CameraOrigin == null)
                    {
                        Plugin.Log?.Warn("Failed to find Origin transform!");
                    }
                }
                else
                {
                    Plugin.Log?.Warn("Failed to find LocalPlayerGameCore game object!");
                }
            }
            
            if (CameraOrigin == null && _loadedSceneNames.Contains(MenuSceneName))
            {
                Scene menuScene = SceneManager.GetSceneByName(MenuSceneName);

                //Plugin.Log?.Warn("[Scene Game Objects]");
                //PluginUtils.PrintObjectTreeInScene(loadedScene);

                GameObject menuCore = PluginUtils.FindGameObjectRecursiveInScene(menuScene, "MenuCore");
                //PluginUtils.PrintComponents(menuCore);
                if (menuCore != null)
                {
                    CameraOrigin = menuCore.transform.Find("Origin");
                    //PluginUtils.PrintComponents(GameOrigin?.gameObject);
                    if (CameraOrigin == null)
                    {
                        Plugin.Log?.Warn("Failed to find Origin transform!");
                    }
                }
                else
                {
                    Plugin.Log?.Warn("Failed to find MenuCore game object!");
                }
            }

            if (CameraOrigin != null)
            {
                _MRCamera.transform.parent = CameraOrigin;
                Plugin.Log?.Warn("Updating camera origin to "+ CameraOrigin.name);
            }
        }

        void SpawnMikanCamera()
        {
            Plugin.Log?.Info("Mikan: Created Mikan Camera");
            GameObject cameraGameObject = new GameObject(
                "MikanCamera",
                new System.Type[] { typeof(Camera) });
            _MRCamera = cameraGameObject.GetComponent<Camera>();
            _MRCamera.stereoTargetEye = StereoTargetEyeMask.None;
            _MRCamera.backgroundColor = new Color(0, 0, 0, 0); 
            _MRCamera.clearFlags = CameraClearFlags.SolidColor;
            _MRCamera.forceIntoRenderTexture = true;
            //_MRCamera.cullingMask = 1 << 9;

            if (_renderTexture != null)
            {
                _MRCamera.targetTexture = _renderTexture;
            }
            else
            {
                createFrameBuffer(256, 256);
            }

            updateCameraProjectionMatrix();
        }

        void DespawnMikanCamera()
        {
            if (_MRCamera != null)
            {
                Plugin.Log?.Info("Mikan: Destroyed Mikan Camera");
                Destroy(_MRCamera.gameObject);
                _MRCamera = null;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (MikanClientAPI.Mikan_GetIsConnected())
            {
                MikanEvent mikanEvent;
                while (MikanClientAPI.Mikan_PollNextEvent(out mikanEvent) == MikanResult.Success)
                {
                    switch(mikanEvent.event_type)
                    {
                    case MikanEventType.connected:
                        Plugin.Log?.Warn("Mikan: Connected!");
                        reallocateRenderBuffers();
                        //setupStencils();
                        updateCameraProjectionMatrix();
                        _connectEvent.Invoke();
                        break;
                    case MikanEventType.disconnected:
                        Plugin.Log?.Warn("Mikan: Disconnected!");
                        _disconnectEvent.Invoke();
                        break;
                    case MikanEventType.videoSourceOpened:
                        reallocateRenderBuffers();
                        updateCameraProjectionMatrix();
                        break;
                    case MikanEventType.videoSourceClosed:
                        break;
                    case MikanEventType.videoSourceNewFrame:
                        processNewVideoSourceFrame(mikanEvent.event_payload.video_source_new_frame);
    					break;
				    case MikanEventType.videoSourceModeChanged:
				    case MikanEventType.videoSourceIntrinsicsChanged:
					   reallocateRenderBuffers();
					   updateCameraProjectionMatrix();
					   break;
				    case MikanEventType.videoSourceAttachmentChanged:
					   break;
				    case MikanEventType.vrDevicePoseUpdated:
					   break;
				    case MikanEventType.anchorPoseUpdated:
                       updateAnchorPose(mikanEvent.event_payload.anchor_pose_updated);
					   break;
				    case MikanEventType.anchorListUpdated:
					   break;
                    }
                }
            }
            else
            {
                if (_mikanReconnectTimeout <= 0.0f)
                {
                    if (MikanClientAPI.Mikan_Connect(_clientInfo) == MikanResult.Success)
                    {
                        _lastReceivedVideoSourceFrame = 0;
                    }
                    else
                    {
                        // Reset reconnect attempt timer
                        _mikanReconnectTimeout = 1.0f;
                    }
                }
                else
                {
                    _mikanReconnectTimeout -= Time.deltaTime;
                }
            }
        }

        void setupStencils()
        {
            // Skip if stencils are already created
            MikanStencilList stencilList;
            MikanClientAPI.Mikan_GetStencilList(out stencilList);
            if (stencilList.stencil_count > 0)
                return;

            // Get the origin spatial anchor to build the stencil scene around
            MikanSpatialAnchorInfo originSpatialAnchor;
            if (MikanClientAPI.Mikan_FindSpatialAnchorInfoByName("origin", out originSpatialAnchor) == MikanResult.Success)
            {
                _originSpatialAnchorXform = MikanMath.MikanMatrix4fToMatrix4x4(originSpatialAnchor.anchor_xform);
            }
            else
            {
                _originSpatialAnchorXform = Matrix4x4.identity;
            }

            // Create a stencil in front of the origin
            {
                Vector4 col0 = _originSpatialAnchorXform.GetColumn(0);
                Vector4 col1 = _originSpatialAnchorXform.GetColumn(1);
                Vector4 col2 = _originSpatialAnchorXform.GetColumn(2);
                Vector4 col3 = _originSpatialAnchorXform.GetColumn(3);

                Vector3 quad_x_axis = new Vector3(col0.x, col0.y, col0.z);
                Vector3 quad_y_axis = new Vector3(col1.x, col1.y, col1.z);
                Vector3 quad_normal = new Vector3(col2.x, col2.y, col2.z);
                Vector3 quad_center = new Vector3(col3.x, col3.y, col3.z) + quad_normal * 0.4f + quad_y_axis * 0.3f;

                _stencilQuad = new MikanStencilQuad();
                _stencilQuad.stencil_id = SDKConstants.INVALID_MIKAN_ID; // filled in on allocation
                _stencilQuad.quad_center = MikanMath.Vector3ToMikanVector3f(quad_center);
                _stencilQuad.quad_x_axis = MikanMath.Vector3ToMikanVector3f(quad_x_axis);
                _stencilQuad.quad_y_axis = MikanMath.Vector3ToMikanVector3f(quad_y_axis);
                _stencilQuad.quad_normal = MikanMath.Vector3ToMikanVector3f(quad_normal);
                _stencilQuad.quad_width = 0.25f;
                _stencilQuad.quad_height = 0.25f;
                _stencilQuad.is_double_sided = true;
                _stencilQuad.is_disabled = false;
                MikanClientAPI.Mikan_AllocateQuadStencil(ref _stencilQuad);
            }
        }

        void processNewVideoSourceFrame(MikanVideoSourceNewFrameEvent newFrameEvent)
	    {
		    if (newFrameEvent.frame == _lastReceivedVideoSourceFrame)
		    	return;

            // Apply the camera pose received
            setCameraPose(
                MikanMath.MikanVector3fToVector3(newFrameEvent.cameraForward),
                MikanMath.MikanVector3fToVector3(newFrameEvent.cameraUp),
                MikanMath.MikanVector3fToVector3(newFrameEvent.cameraPosition));

            // Render out a new frame
            render(newFrameEvent.frame);

            // Remember the frame index of the last frame we published
            _lastReceivedVideoSourceFrame = newFrameEvent.frame;
        }

        void setCameraPose(Vector3 cameraForward, Vector3 cameraUp, Vector3 cameraPosition)
        {
            if (_MRCamera == null)
                return;

            // Decompose Matrix4x4 into a quaternion and an position
            _MRCamera.transform.localRotation = Quaternion.LookRotation(cameraForward, cameraUp);
            _MRCamera.transform.localPosition = cameraPosition;

            //Plugin.Log?.Error($"Mikan: New camera position {cameraPosition.x},{cameraPosition.y},{cameraPosition.z}");
        }

        void reallocateRenderBuffers()
        {
            freeFrameBuffer();

            MikanClientAPI.Mikan_FreeRenderTargetBuffers();
            _renderTargetMemory = new MikanRenderTargetMemory();

            MikanVideoSourceMode mode;
            if (MikanClientAPI.Mikan_GetVideoSourceMode(out mode) == MikanResult.Success)
            {
                MikanRenderTargetDescriptor desc;
                desc.width = (uint)mode.resolution_x;
                desc.height = (uint)mode.resolution_y;
                desc.color_key = new MikanColorRGB() { 
                    r= BackgroundColorKey.r, 
                    g= BackgroundColorKey.g, 
                    b= BackgroundColorKey.b
                };
                desc.color_buffer_type = MikanColorBufferType.RGBA32;
                desc.depth_buffer_type = MikanDepthBufferType.NONE;
				desc.graphicsAPI = _clientInfo.graphicsAPI;
				
                if (MikanClientAPI.Mikan_AllocateRenderTargetBuffers(desc, out _renderTargetMemory) != MikanResult.Success)
                {
                    Plugin.Log?.Error("Mikan: Failed to allocate render target buffers");
                }

                createFrameBuffer(mode.resolution_x, mode.resolution_y);

            }
            else
            {
                Plugin.Log?.Error("Mikan: Failed to get video source mode");
            }
        }

        bool createFrameBuffer(int width, int height)
        {
            bool bSuccess = true;

            if (width <= 0 || height <= 0)
            {
                Plugin.Log?.Error("Mikan: Unable to create render texture. Texture dimension must be higher than zero.");
                return false;
            }

            int depthBufferPrecision = 0;
            _renderTexture = new RenderTexture(width, height, depthBufferPrecision, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                anisoLevel = 0
            };

            if (!_renderTexture.Create())
            {
                Plugin.Log?.Error("Mikan: Unable to create render texture.");
                return false;
            }

            if (_MRCamera != null)
            {
                _MRCamera.targetTexture = _renderTexture;
            }

            Plugin.Log?.Info($"Mikan: Created {width}x{height} render target texture");

            return bSuccess;
        }

        void freeFrameBuffer()
        {
            if (_renderTexture == null) return;

            if (_MRCamera != null)
            {
                _MRCamera.targetTexture = null;
            }

            if (_renderTexture.IsCreated())
            {
                _renderTexture.Release();
            }
            _renderTexture = null;
        }

        void updateCameraProjectionMatrix()
        {
            if (MikanClientAPI.Mikan_GetIsConnected())
            {
                MikanVideoSourceIntrinsics videoSourceIntrinsics;
                if (MikanClientAPI.Mikan_GetVideoSourceIntrinsics(out videoSourceIntrinsics) == MikanResult.Success)
                {
                    MikanMonoIntrinsics monoIntrinsics = videoSourceIntrinsics.intrinsics.mono;
                    float videoSourcePixelWidth = (float)monoIntrinsics.pixel_width;
                    float videoSourcePixelHeight = (float)monoIntrinsics.pixel_height;

                    if (_MRCamera != null)
                    {
                        _MRCamera.fieldOfView = (float)monoIntrinsics.vfov;
                        _MRCamera.aspect = videoSourcePixelWidth / videoSourcePixelHeight;
                        _MRCamera.nearClipPlane = (float)monoIntrinsics.znear;
                        _MRCamera.farClipPlane = (float)monoIntrinsics.zfar;

                        Plugin.Log?.Info($"Mikan: Updated camera params: fov:{_MRCamera.fieldOfView}, aspect:{_MRCamera.aspect}, near:{_MRCamera.nearClipPlane}, far:{_MRCamera.farClipPlane}");
                    }
                }
            }
        }

        void updateAnchorPose(MikanAnchorPoseUpdateEvent anchorPoseEvent)
        {
            MikanPoseUpdateEvent anchorEvent;

            if (_anchorPoseEvents.TryGetValue(anchorPoseEvent.anchor_id, out anchorEvent))
            {
                anchorEvent.Invoke(anchorPoseEvent.transform);
            }
        }

        public void addAnchorPoseListener(MikanSpatialAnchorID anchor_id, UnityAction<MikanMatrix4f> call)
        {
            MikanPoseUpdateEvent anchorEvent;

            if (!_anchorPoseEvents.TryGetValue(anchor_id, out anchorEvent))
            {
                anchorEvent = new MikanPoseUpdateEvent();
                _anchorPoseEvents.Add(anchor_id, anchorEvent);
            }

            anchorEvent.AddListener(call);
        }

        public void removeAnchorPoseListener(MikanSpatialAnchorID anchor_id, UnityAction<MikanMatrix4f> call)
        {
            MikanPoseUpdateEvent anchorEvent;

            if (_anchorPoseEvents.TryGetValue(anchor_id, out anchorEvent))
            {
                anchorEvent.RemoveListener(call);

                if (anchorEvent.GetPersistentEventCount() == 0)
                {
                    _anchorPoseEvents.Remove(anchor_id);
                }
            }
        }

        void render(ulong frame_index)
        {
            _lastRenderedFrame = frame_index;

            if (_MRCamera != null)
            {
                //foreach (Transform transform in BeatSaberDMXController.Instance.ColorANotes)
                //{
                //    PluginUtils.SetGameObjectLayerRecursive(transform.gameObject, 9);
                //}

                _MRCamera.Render();

                //foreach (Transform transform in BeatSaberDMXController.Instance.ColorANotes)
                //{
                //    PluginUtils.SetGameObjectLayerRecursive(transform.gameObject, 0);
                //}
            }

            if (_clientInfo.graphicsAPI == MikanClientGraphicsAPI.Direct3D11 ||
                _clientInfo.graphicsAPI == MikanClientGraphicsAPI.OpenGL)
            {
                IntPtr textureNativePtr = _renderTexture.GetNativeTexturePtr();

                // Fast interprocess shared texture transfer
                MikanClientAPI.Mikan_PublishRenderTargetTexture(textureNativePtr, frame_index);
            }
            else if (_renderTargetMemory.color_buffer != IntPtr.Zero)
            {
                _readbackRequest= AsyncGPUReadback.Request(_renderTexture, 0, ReadbackCompleted);
            }
        }

        void ReadbackCompleted(AsyncGPUReadbackRequest request)
        {
            if (!request.hasError)
            {
                NativeArray<byte> buffer = request.GetData<byte>();

                if (buffer.Length > 0 &&
                    _renderTargetMemory.color_buffer != IntPtr.Zero &&
                    _renderTargetMemory.color_buffer_size.ToUInt32() == buffer.Length)
                {
                    unsafe
                    {
                        void* dest = _renderTargetMemory.color_buffer.ToPointer();
                        void* source = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
                        long size = buffer.Length;

                        UnsafeUtility.MemCpy(dest, source, size);
                    }

                    // Publish the new video frame back to Mikan
                    MikanClientAPI.Mikan_PublishRenderTargetBuffers(_lastRenderedFrame);
                }
            }
        }
    }
}