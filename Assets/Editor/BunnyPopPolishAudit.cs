#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public static class BunnyPopPolishAudit
{
    public static void Navigation()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        foreach(var obstacle in Object.FindObjectsByType<NavMeshObstacle>()) obstacle.enabled=false;
        foreach(var surface in Object.FindObjectsByType<NavMeshSurface>()) { surface.RemoveData(); surface.AddData(); }
        var filter=new NavMeshQueryFilter { agentTypeID=0,areaMask=NavMesh.AllAreas };
        var p=Object.FindAnyObjectByType<BunnyController>();
        var terrain=Terrain.activeTerrain;
        NavMesh.SamplePosition(p.transform.position,out var start,15,filter);
        var path=new NavMeshPath(); int samples=0,reachable=0;
        var bounds=new Bounds(start.position,Vector3.zero);
        for(float x=10;x<350;x+=10) for(float z=10;z<600;z+=10)
        {
            Vector3 point=new Vector3(x,0,z); point.y=terrain.SampleHeight(point);
            if(!NavMesh.SamplePosition(point,out var hit,4,filter)) continue;
            samples++;
            if(!NavMesh.CalculatePath(start.position,hit.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            reachable++; bounds.Encapsulate(hit.position);
        }
        Debug.Log($"NAV_AUDIT start={start.position} samples={samples} reachable={reachable} bounds={bounds}");
        foreach(var s in Object.FindObjectsByType<Seed>())
        {
            Vector3 pos=s.transform.position; pos.y=terrain.SampleHeight(pos);
            bool sampled=NavMesh.SamplePosition(pos,out var hit,10,filter);
            bool result=sampled&&NavMesh.CalculatePath(start.position,hit.position,filter,path);
            Debug.Log($"NAV_SEED {s.name} {pos} sampled={sampled} result={result} status={path.status}");
        }
    }
    public static void Run()
    {
        foreach (var name in new[] { "MainMenu", "SampleScene" })
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            Debug.Log("AUDIT_SCENE " + name);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                var s = c.GetComponent<UnityEngine.UI.CanvasScaler>();
                Debug.Log($"AUDIT_CANVAS {Path(c.transform)} enabled={c.enabled} scale={s?.uiScaleMode} reference={s?.referenceResolution}");
                foreach (var r in c.GetComponentsInChildren<RectTransform>(true))
                {
                    if (r.GetComponent<TMPro.TMP_Text>() != null && r.name.StartsWith("Text")) continue;
                    var button = r.GetComponent<UnityEngine.UI.Button>();
                    var events = button == null ? "" : string.Join(",", Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Select(i => button.onClick.GetPersistentMethodName(i)));
                    Debug.Log($"AUDIT_UI {Path(r)} active={r.gameObject.activeSelf} size={r.sizeDelta} pos={r.anchoredPosition} anchors={r.anchorMin}/{r.anchorMax} button={events}");
                }
            }
            foreach (var e in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include))
            {
                var a=e.GetComponent<NavMeshAgent>(); var b=e.GetComponentInChildren<Collider>();
                Debug.Log($"AUDIT_ENEMY {Path(e.transform)} active={e.gameObject.activeInHierarchy} pos={e.transform.position} scale={e.transform.lossyScale} speed={a.speed}/{e.chaseSpeed} offset={a.baseOffset} radius={a.radius} bounds={b?.bounds} animator={e.GetComponent<Animator>()?.runtimeAnimatorController?.name}");
            }
            foreach(var s in Object.FindObjectsByType<Seed>(FindObjectsInactive.Include))
                Debug.Log($"AUDIT_SEED {Path(s.transform)} active={s.gameObject.activeInHierarchy} pos={s.transform.position} charges={s.carrotCharges}");
            foreach(var s in Object.FindObjectsByType<NavMeshSurface>())
                Debug.Log($"AUDIT_NAV {s.name} type={s.agentTypeID} geometry={s.useGeometry} collect={s.collectObjects} data={AssetDatabase.GetAssetPath(s.navMeshData)}");
            foreach(var t in Terrain.activeTerrains) Debug.Log($"AUDIT_TERRAIN {t.transform.position} size={t.terrainData.size}");
            foreach(var g in Object.FindObjectsByType<ForestGate>()) Debug.Log($"AUDIT_GATE pos={g.transform.position} forward={g.transform.forward} scale={g.transform.lossyScale} sentry={g.sentry.transform.position}");
            var p=Object.FindAnyObjectByType<BunnyController>();
            if(p!=null) Debug.Log($"AUDIT_PLAYER {p.transform.position} scale={p.transform.lossyScale} bounds={p.controller.bounds} cam={p.cameraTransform?.name} parent={Camera.main?.transform.parent?.name} pivot={Object.FindAnyObjectByType<CameraController>()?.cameraPivot?.parent?.name}");
        }
    }
    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent)+"/"+t.name;
}
#endif
