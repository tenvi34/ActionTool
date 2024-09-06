using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class PrefabDeleteCallbackProcessor : AssetModificationProcessor
{
    private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
    {
        // 삭제되는 에셋이 프리팹인지 확인
        if (Path.GetExtension(assetPath) == ".prefab")
        {
            // 프리팹 에셋 로드
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
            {
                ActionScript actionScript = prefab.GetComponent<ActionScript>();
                if (actionScript)
                {
                    foreach (var actionScriptActionEvent in actionScript.actionEvents)
                    {
                        string path = AssetDatabase.GetAssetPath(actionScriptActionEvent.eventData);
                        AssetDatabase.DeleteAsset(path);
                    }
                }
                
                Debug.Log($"프리팹이 삭제되었습니다: {assetPath}");

                // 여기에 프리팹 삭제 후 수행할 작업을 추가하세요
                PerformPostDeleteActions(assetPath);
            }
        }

        // 기본적으로 삭제를 허용
        return AssetDeleteResult.DidNotDelete;
    }

    private static void PerformPreDeleteActions(GameObject prefab)
    {
        // 프리팹 삭제 전 수행할 작업을 여기에 구현
        // 예: 프리팹과 연관된 데이터 정리, 로그 기록 등
        Debug.Log($"프리팹 '{prefab.name}'이(가) 삭제되기 전 작업 수행");
    }

    private static void PerformPostDeleteActions(string assetPath)
    {
        // 프리팹 삭제 후 수행할 작업을 여기에 구현
        // 예: 관련 설정 업데이트, 캐시 정리 등
        Debug.Log($"프리팹 '{Path.GetFileNameWithoutExtension(assetPath)}'이(가) 삭제된 후 작업 수행");
    }
}