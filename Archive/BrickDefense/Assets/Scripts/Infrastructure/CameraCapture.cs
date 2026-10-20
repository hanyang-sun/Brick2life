using System;
using System.Collections;
using BrickDefense.Core;
using UnityEngine;

namespace BrickDefense.Infrastructure
{
    // 管理相机权限、预览与拍摄；图像方向归一交给 PhotoPixels。
    public sealed class CameraCapture : MonoBehaviour
    {
        public WebCamTexture Texture { get; private set; }
        public bool IsStarting { get; private set; }
        public event Action<string> StatusChanged;

        public void Begin()
        {
            if (IsStarting) return;
            Stop(); IsStarting=true; StartCoroutine(StartCamera());
        }
        private IEnumerator StartCamera()
        {
            try { yield return CameraSession(); }
            finally { IsStarting=false; }
        }
        private IEnumerator CameraSession()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                bool answered=false,granted=false;
                var callbacks=new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted+=_ => { answered=true; granted=true; };
                callbacks.PermissionDenied+=_ => { answered=true; };
                callbacks.PermissionDeniedAndDontAskAgain+=_ => { answered=true; };
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera,callbacks);
                float deadline=Time.realtimeSinceStartup+60;
                while (!answered && Time.realtimeSinceStartup<deadline) yield return null;
                if (!granted) { StatusChanged?.Invoke("Camera permission was not granted."); yield break; }
            }
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            { StatusChanged?.Invoke("Camera permission was not granted."); yield break; }
#endif
            WebCamDevice[] devices=WebCamTexture.devices;
            if (devices.Length==0) { StatusChanged?.Invoke("No camera found. Import a photo instead."); yield break; }
            string name=devices[0].name;
            foreach (WebCamDevice device in devices) if (!device.isFrontFacing) { name=device.name; break; }
            Texture=new WebCamTexture(name,1920,1080,15); Texture.Play();
            StatusChanged?.Invoke("Keep every cube fully visible on a plain background and photograph from above.");
            float timeout=Time.realtimeSinceStartup+12;
            while (Texture!=null && (!Texture.didUpdateThisFrame || Texture.width<=16) && Time.realtimeSinceStartup<timeout)
                yield return null;
            if (Texture!=null && Texture.width<=16)
            {
                Texture.Stop(); Destroy(Texture); Texture=null;
                StatusChanged?.Invoke("Camera did not start. Check device permissions or import a photo.");
            }
        }
        public ImageFrame Capture()
        {
            if (Texture==null || !Texture.isPlaying || Texture.width<=16) throw new InvalidOperationException("The camera is not ready.");
            return PhotoPixels.FromUnity("frame-"+Guid.NewGuid().ToString("N"),Texture.GetPixels32(),Texture.width,Texture.height,
                Texture.videoRotationAngle,Texture.videoVerticallyMirrored);
        }
        public void Stop()
        {
            StopAllCoroutines(); IsStarting=false;
            if (Texture!=null) { Texture.Stop(); Destroy(Texture); Texture=null; }
        }
        private void OnDisable() { Stop(); }
    }
}
