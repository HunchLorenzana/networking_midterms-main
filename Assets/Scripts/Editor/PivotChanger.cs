
using UnityEngine;
using UnityEditor;

public class PivotChanger : EditorWindow
{
    private Vector3 _newPivotPosition = Vector3.zero;
    private GameObject _lastSelectedObject;

    [MenuItem("Tools/Pivot Changer")]
    public static void OpenWindow()
    {
        GetWindow<PivotChanger>("Pivot Changer");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        GUILayout.Label("Pivot Changer", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Change the pivot while preserving the visible mesh, " +
            "child objects, and supported colliders.",
            MessageType.Info
        );

        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject != _lastSelectedObject)
        {
            _lastSelectedObject = selectedObject;
            _newPivotPosition = Vector3.zero;
            SceneView.RepaintAll();
        }

        if (selectedObject == null)
        {
            EditorGUILayout.HelpBox(
                "Select a GameObject in the Hierarchy.",
                MessageType.Warning
            );
            return;
        }

        MeshFilter meshFilter =
            selectedObject.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            EditorGUILayout.HelpBox(
                "The selected object needs a MeshFilter " +
                "with a valid mesh.",
                MessageType.Warning
            );
            return;
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Selected Object",
            selectedObject.name
        );

        EditorGUI.BeginChangeCheck();

        _newPivotPosition = EditorGUILayout.Vector3Field(
            "New Pivot Position (Local)",
            _newPivotPosition
        );

        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Set Pivot", GUILayout.Height(35)))
        {
            ChangePivot(selectedObject, _newPivotPosition);
        }

        if (GUILayout.Button("Reset Input"))
        {
            _newPivotPosition = Vector3.zero;
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "Yellow = current pivot. Green = proposed pivot. " +
            "The preview does not modify the object.",
            MessageType.Info
        );
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
            return;

        MeshFilter meshFilter =
            selectedObject.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        Transform targetTransform = selectedObject.transform;

        Vector3 currentPivot = targetTransform.position;

        Vector3 previewPivot =
            targetTransform.TransformPoint(_newPivotPosition);

        float handleSize =
            HandleUtility.GetHandleSize(previewPivot);

        Handles.zTest =
            UnityEngine.Rendering.CompareFunction.Always;

        Handles.color = Color.green;

        Handles.DrawDottedLine(
            currentPivot,
            previewPivot,
            4f
        );

        Handles.color = Color.yellow;

        Handles.SphereHandleCap(
            0,
            currentPivot,
            Quaternion.identity,
            handleSize * 0.12f,
            EventType.Repaint
        );

        Handles.Label(
            currentPivot + Vector3.up * handleSize * 0.2f,
            "Current Pivot"
        );

        Handles.color = Color.green;

        Handles.SphereHandleCap(
            0,
            previewPivot,
            Quaternion.identity,
            handleSize * 0.18f,
            EventType.Repaint
        );

        Handles.Label(
            previewPivot + Vector3.up * handleSize * 0.25f,
            "New Pivot"
        );

        Handles.zTest =
            UnityEngine.Rendering.CompareFunction.LessEqual;
    }

    private static void ChangePivot(
        GameObject target,
        Vector3 newPivotPosition)
    {
        Transform targetTransform = target.transform;

        MeshFilter meshFilter =
            target.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning(
                "Pivot Changer: No valid MeshFilter."
            );
            return;
        }

        // Create a separate mesh copy.
        Mesh originalMesh = meshFilter.sharedMesh;

        Mesh newMesh = Instantiate(originalMesh);
        newMesh.name = originalMesh.name + "_PivotChanged";

        Undo.RegisterCreatedObjectUndo(
            newMesh,
            "Create Pivot Changed Mesh"
        );

        Undo.RecordObject(
            targetTransform,
            "Change Object Pivot"
        );

        Undo.RecordObject(
            meshFilter,
            "Change Mesh Pivot"
        );

        // Preserve direct children in world space.
        Transform[] children =
            new Transform[targetTransform.childCount];

        Vector3[] childWorldPositions =
            new Vector3[children.Length];

        for (int i = 0; i < children.Length; i++)
        {
            children[i] = targetTransform.GetChild(i);

            childWorldPositions[i] =
                children[i].position;

            Undo.RecordObject(
                children[i],
                "Preserve Child Position"
            );
        }

        // Shift the visual mesh.
        Vector3[] vertices = newMesh.vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] -= newPivotPosition;
        }

        newMesh.vertices = vertices;
        newMesh.RecalculateBounds();

        meshFilter.sharedMesh = newMesh;

        // Move the object transform to compensate.
        Vector3 worldOffset =
            targetTransform.TransformVector(newPivotPosition);

        targetTransform.position += worldOffset;

        // Preserve colliders attached directly to this object.
        Collider[] colliders =
            target.GetComponents<Collider>();

        foreach (Collider collider in colliders)
        {
            Undo.RecordObject(
                collider,
                "Preserve Collider Position"
            );

            // Box Collider.
            BoxCollider boxCollider =
                collider as BoxCollider;

            if (boxCollider != null)
            {
                boxCollider.center -= newPivotPosition;
                continue;
            }

            // Sphere Collider.
            SphereCollider sphereCollider =
                collider as SphereCollider;

            if (sphereCollider != null)
            {
                sphereCollider.center -= newPivotPosition;
                continue;
            }

            // Capsule Collider.
            CapsuleCollider capsuleCollider =
                collider as CapsuleCollider;

            if (capsuleCollider != null)
            {
                capsuleCollider.center -= newPivotPosition;
                continue;
            }

            // Character Controller.
            CharacterController characterController =
                collider as CharacterController;

            if (characterController != null)
            {
                characterController.center -= newPivotPosition;
                continue;
            }

            // Mesh Collider.
            MeshCollider meshCollider =
                collider as MeshCollider;

            if (meshCollider != null)
            {
                Mesh colliderMesh = meshCollider.sharedMesh;

                if (colliderMesh == null)
                    continue;

                // If the collider uses the same mesh as the
                // MeshFilter, reuse the already shifted mesh.
                if (colliderMesh == originalMesh)
                {
                    meshCollider.sharedMesh = newMesh;
                    continue;
                }

                // Otherwise, create a separate shifted copy.
                Mesh newColliderMesh =
                    Instantiate(colliderMesh);

                newColliderMesh.name =
                    colliderMesh.name + "_PivotChanged";

                Undo.RegisterCreatedObjectUndo(
                    newColliderMesh,
                    "Create Pivot Changed Collider Mesh"
                );

                Vector3[] colliderVertices =
                    newColliderMesh.vertices;

                for (int i = 0; i < colliderVertices.Length; i++)
                {
                    colliderVertices[i] -= newPivotPosition;
                }

                newColliderMesh.vertices = colliderVertices;
                newColliderMesh.RecalculateBounds();

                meshCollider.sharedMesh = newColliderMesh;
            }
        }

        // Restore child world positions.
        for (int i = 0; i < children.Length; i++)
        {
            children[i].position = childWorldPositions[i];
        }

        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(targetTransform);
        EditorUtility.SetDirty(meshFilter);

        foreach (Collider collider in colliders)
        {
            EditorUtility.SetDirty(collider);
        }

        Debug.Log(
            "Pivot Changer: Pivot changed. Mesh, child positions, " +
            "and supported colliders preserved for " + target.name,
            target
        );
    }
}
