using System.Collections.Generic;
using BrickDefense.Core;
using UnityEngine;

namespace BrickDefense.Presentation
{
    // 根据关卡结果创建 Unity 预览对象；这里不参与照片识别或寻路决策。
    public sealed class LevelView : MonoBehaviour
    {
        private Camera sceneCamera;
        private BoardOrbitCamera orbit;
        private Transform boardRoot,entityRoot,routeRoot;
        private Material lightTile,darkTile,coreMaterial,wallMaterial,towerMaterial,routeMaterial,rejectedMaterial,baseMaterial,tableMaterial;
        private IReadOnlyList<SpawnRoute> entrances;
        private GUIStyle spawnLabel;

        private void Awake()
        {
            sceneCamera=Camera.main;
            if (sceneCamera == null)
            {
                var cameraObject=new GameObject("Demo camera"); cameraObject.tag="MainCamera";
                cameraObject.transform.SetParent(transform); sceneCamera=cameraObject.AddComponent<Camera>();
            }
            sceneCamera.orthographic=true; orbit=new BoardOrbitCamera(sceneCamera);
            sceneCamera.backgroundColor=SoftStrategyTheme.Background; sceneCamera.clearFlags=CameraClearFlags.SolidColor;
            var lightObject=new GameObject("Demo light"); lightObject.transform.SetParent(transform);
            var light=lightObject.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=0.7f;
            light.shadows=LightShadows.Soft; light.shadowStrength=0.28f; light.shadowBias=0.035f;
            lightObject.transform.rotation=Quaternion.Euler(48,-38,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(0.50f,0.50f,0.50f);
            lightTile=MakeMaterial(new Color32(199,212,177,255)); darkTile=MakeMaterial(new Color32(195,207,172,255));
            coreMaterial=MakeMaterial(SoftStrategyTheme.Core); wallMaterial=MakeMaterial(SoftStrategyTheme.Wall);
            towerMaterial=MakeMaterial(SoftStrategyTheme.Tower); routeMaterial=MakeMaterial(SoftStrategyTheme.Route);
            rejectedMaterial=MakeMaterial(SoftStrategyTheme.Danger);
            baseMaterial=MakeMaterial(new Color32(213,201,165,255)); tableMaterial=MakeMaterial(SoftStrategyTheme.Background);
            Transform setting=NewRoot("Board setting");
            SettingCube(setting,"Board base",new Vector3(0,-0.36f,0),new Vector3(20.65f,0.5f,20.65f),baseMaterial);
            SettingCube(setting,"Table",new Vector3(0,-0.72f,0),new Vector3(200,0.12f,200),tableMaterial);
            // 20×20 底板只创建一次，切换关卡时仅替换积木与路线对象。
            boardRoot=NewRoot("Navigation grid");
            for (int y=0; y<BoardSize.Navigation; y++)
                for (int x=0; x<BoardSize.Navigation; x++)
                {
                    Vector3 center=WorldForNavigation(new GridPoint(x,y));
                    var tile=GameObject.CreatePrimitive(PrimitiveType.Cube); tile.name=$"Cell {x},{y}";
                    tile.transform.SetParent(boardRoot); tile.transform.position=new Vector3(center.x,-0.06f,center.z);
                    tile.transform.localScale=new Vector3(0.975f,0.1f,0.975f);
                    Renderer renderer=tile.GetComponent<Renderer>(); renderer.sharedMaterial=(x+y)%2==0?lightTile:darkTile;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
        }

        // 实体格中心间距为 2 个世界单位，恰好覆盖其 2×2 导航格。
        public void Display(LevelResult level)
        {
            ReplaceRoot(ref entityRoot,"Physical bricks"); ReplaceRoot(ref routeRoot,"Route preview");
            foreach (LevelEntity entity in level.Entities)
            {
                var brick=GameObject.CreatePrimitive(PrimitiveType.Cube); brick.name=$"{entity.Role} {entity.BrickId}";
                brick.transform.SetParent(entityRoot); brick.transform.position=new Vector3(2*entity.C-9f,0.66f,9f-2*entity.R);
                brick.transform.localScale=new Vector3(1.84f,1.3f,1.84f);
                brick.GetComponent<Renderer>().sharedMaterial=entity.Role==BrickRole.Core?coreMaterial:
                    entity.Role==BrickRole.Wall?wallMaterial:towerMaterial;
            }
            entrances=level.SpawnRoutes;
            var shown=new HashSet<GridPoint>();
            for(int i=0;i<entrances.Count;i++)
            {
                SpawnRoute entrance=entrances[i];
                foreach(GridPoint point in entrance.Route)
                    if(shown.Add(point)) Marker($"Route {point.X},{point.Y}",point,0.08f,new Vector3(0.23f,0.035f,0.23f),routeMaterial);
                GridPoint spawn=entrance.Spawn;
                if(spawn.X>=0 && spawn.Y>=0 && spawn.X<BoardSize.Navigation && spawn.Y<BoardSize.Navigation)
                    Marker($"Selected spawn {i+1}",spawn,entrance.HasRoute?0.22f:1.47f,new Vector3(0.75f,0.15f,0.75f),
                        entrance.HasRoute?routeMaterial:rejectedMaterial);
            }
        }

        public void DisplayDraft(IReadOnlyList<BrickObservation> bricks,System.Func<string,BrickRole> roleFor)
        {
            var entities=new LevelEntity[bricks.Count];
            for(int i=0;i<entities.Length;i++) entities[i]=new LevelEntity(new GridPlacement(bricks[i]),roleFor(bricks[i].ColorId));
            Display(new LevelResult(ResultStatus.Success,entities,System.Array.Empty<LevelCell>(),
                System.Array.Empty<string>(),System.Array.Empty<SpawnRoute>(),System.Array.Empty<Issue>()));
        }

        // Drawn by DemoUI before its cards, so numbers disappear on the photo/configuration pages.
        public void DrawSpawnLabels(float guiScale)
        {
            if(entrances==null) return;
            spawnLabel??=new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=14 };
            spawnLabel.normal.textColor=Color.white;
            for(int i=0;i<entrances.Count;i++)
            {
                SpawnRoute entry=entrances[i];
                Vector3 pixel=sceneCamera.WorldToScreenPoint(WorldForNavigation(entry.Spawn)+Vector3.up*(entry.HasRoute?0.32f:1.57f));
                if(pixel.z>0) GUI.Label(new Rect(pixel.x/guiScale-18,(Screen.height-pixel.y)/guiScale-10,36,20),(i+1).ToString(),spawnLabel);
            }
        }

        // GUI 原点在左上，射线坐标在左下；先翻转 Y，再与棋盘水平面求交。
        public bool TryPickCell(Vector2 guiPosition,out GridPoint cell)
        {
            cell=default;
            Ray ray=sceneCamera.ScreenPointToRay(new Vector3(guiPosition.x,Screen.height-guiPosition.y,0));
            var plane=new Plane(Vector3.up,Vector3.zero);
            if (!plane.Raycast(ray,out float distance)) return false;
            Vector3 hit=ray.GetPoint(distance); int x=Mathf.FloorToInt(hit.x+10),y=Mathf.FloorToInt(10-hit.z);
            if (x<0 || y<0 || x>=BoardSize.Navigation || y>=BoardSize.Navigation) return false;
            cell=new GridPoint(x,y); return true;
        }

        private void Marker(string name,GridPoint cell,float height,Vector3 scale,Material material)
        {
            var marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder); marker.name=name;
            marker.transform.SetParent(routeRoot); marker.transform.position=WorldForNavigation(cell)+Vector3.up*height;
            marker.transform.localScale=scale; marker.GetComponent<Renderer>().sharedMaterial=material;
        }
        private Transform NewRoot(string name) { var root=new GameObject(name).transform; root.SetParent(transform); return root; }
        private void ReplaceRoot(ref Transform root,string name)
        { if (root!=null) { root.gameObject.SetActive(false); Destroy(root.gameObject); } root=NewRoot(name); }
        private void LateUpdate() { FrameBoard(); }
        private void FrameBoard() { orbit.Apply(); }
        public void RotateView(Vector2 screenDelta)
        { orbit.Rotate(180*screenDelta.x/Mathf.Max(Screen.width,1),180*screenDelta.y/Mathf.Max(Screen.height,1)); }
        public void TurnView(float yawDegrees,float pitchDegrees=0) { orbit.Rotate(yawDegrees,pitchDegrees); }
        public float ZoomFactor=>orbit.ZoomFactor;
        public void ZoomView(float steps) { orbit.Zoom(steps); }
        public void ResetView() { orbit.Reset(); }
        private static void SettingCube(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube); part.name=name; part.transform.SetParent(parent);
            part.transform.position=position; part.transform.localScale=scale;
            part.GetComponent<Renderer>().sharedMaterial=material;
        }
        private static Material MakeMaterial(Color color)
        { var material=new Material(Shader.Find("Standard")); material.color=color; material.SetFloat("_Glossiness",0.08f); return material; }
        private static Vector3 WorldForNavigation(GridPoint point) => new Vector3(point.X-9.5f,0,9.5f-point.Y);
        private void OnDestroy()
        {
            foreach (Material material in new[] {lightTile,darkTile,coreMaterial,wallMaterial,towerMaterial,routeMaterial,rejectedMaterial,baseMaterial,tableMaterial})
                if (material!=null) Destroy(material);
        }
    }
}
