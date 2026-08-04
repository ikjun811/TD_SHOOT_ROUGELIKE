using UnityEngine;
using UnityEditor;
using JU.CharacterSystem.AI;
namespace JUTPSEditor
{
    [CustomEditor(typeof(JU_AI_PatrolCharacter))]
    public class JU_AI_PatrolCharacterEditor : Editor
    {
        private JU_AI_PatrolCharacter _patrolAI;

        private SerializedProperty _moveEnabled;
        private SerializedProperty _navigationSettings;
        private SerializedProperty _overrideTarget;
        private SerializedProperty _head;
        private SerializedProperty _general;
        private SerializedProperty _aimSpeed;
        private SerializedProperty _fieldOfView;
        private SerializedProperty _hearSensor;
        private SerializedProperty _patrolPath;
        private SerializedProperty _patrolArea;
        private SerializedProperty _patrolRandomlyIfNotHavePath;
        private SerializedProperty _patrolOnHearSoundBehavior;
        private SerializedProperty _moveRandom;
        private SerializedProperty _followPatrolPath;
        private SerializedProperty _moveRandomPatrolArea;
        private SerializedProperty _moveToPossibleTargetPosition;
        private SerializedProperty _searchLosedTarget;
        private SerializedProperty _damageDetector;
        private SerializedProperty _attack;
        private SerializedProperty _escapeAreas;

        private bool _showPatrolAI;
        private bool _showSensors;
        private bool _showPatrolAreas;
        private bool _showStates;

        void OnEnable()
        {
            _patrolAI = (JU_AI_PatrolCharacter)target;

            // Load states
            _showPatrolAI = EditorPrefs.GetBool($"{nameof(JU_AI_PatrolCharacterEditor)}.{nameof(_showPatrolAI)}");
            _showSensors = EditorPrefs.GetBool($"{nameof(JU_AI_PatrolCharacterEditor)}.{nameof(_showSensors)}");
            _showPatrolAreas = EditorPrefs.GetBool($"{nameof(JU_AI_PatrolCharacterEditor)}.{nameof(_showPatrolAreas)}");
            _showStates = EditorPrefs.GetBool($"{nameof(JU_AI_PatrolCharacterEditor)}.{nameof(_showStates)}");

            _moveEnabled = serializedObject.FindProperty(nameof(_patrolAI.MoveEnabled));
            _navigationSettings = serializedObject.FindProperty(nameof(_patrolAI.NavigationSettings));
            _overrideTarget = serializedObject.FindProperty(nameof(_patrolAI.OverrideTarget));
            _head = serializedObject.FindProperty(nameof(_patrolAI.Head));
            _general = serializedObject.FindProperty(nameof(_patrolAI.General));
            _aimSpeed = serializedObject.FindProperty(nameof(_patrolAI.AimSpeed));

            _fieldOfView = serializedObject.FindProperty(nameof(_patrolAI.FieldOfView));
            _hearSensor = serializedObject.FindProperty(nameof(_patrolAI.HearSensor));

            _patrolPath = serializedObject.FindProperty(nameof(_patrolAI.PatrolPath));
            _patrolArea = serializedObject.FindProperty(nameof(_patrolAI.PatrolArea));

            _patrolRandomlyIfNotHavePath = serializedObject.FindProperty(nameof(_patrolAI.PatrolRandomlyIfNotHavePath));
            _patrolOnHearSoundBehavior = serializedObject.FindProperty(nameof(_patrolAI.OnHearSoundBehavior));
            _moveRandom = serializedObject.FindProperty(nameof(_patrolAI.MoveRandom));
            _followPatrolPath = serializedObject.FindProperty(nameof(_patrolAI.FollowPatrolPath));
            _moveRandomPatrolArea = serializedObject.FindProperty(nameof(_patrolAI.MoveRandomPatrolArea));
            _moveToPossibleTargetPosition = serializedObject.FindProperty(nameof(_patrolAI.MoveToPossibleTargetPosition));
            _searchLosedTarget = serializedObject.FindProperty(nameof(_patrolAI.SearchLosedTarget));
            _damageDetector = serializedObject.FindProperty(nameof(_patrolAI.DamageDetector));
            _attack = serializedObject.FindProperty(nameof(_patrolAI.Attack));
            _escapeAreas = serializedObject.FindProperty(nameof(_patrolAI.EscapeAreas));
        }

        private void OnDisable()
        {
            // Save states
            EditorPrefs.SetBool($"{nameof(JU_AI_PatrolCharacterEditor)}.{nameof(_showPatrolAI)}", _showPatrolAI);
            EditorPrefs.SetBool($"{nameof(JU_AI_PatrolCharacterEditor)}.{nameof(_showSensors)}", _showSensors);
            EditorPrefs.SetBool($"{nameof(JU_AI_PatrolCharacterEditor)}.{nameof(_showPatrolAreas)}", _showPatrolAreas);
            EditorPrefs.SetBool($"{nameof(JU_AI_PatrolCharacterEditor)}.{nameof(_showStates)}", _showStates);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUIStyle toolbarStyle = JUTPSEditor.CustomEditorStyles.Toolbar();

            JUTPSEditor.CustomEditorUtilities.JUTPSTitle("Patrol AI Behaviour");

            // Patrol AI
            _showPatrolAI = GUILayout.Toggle(_showPatrolAI, "Patrol AI", toolbarStyle);
            if (_showPatrolAI)
            {
                EditorGUILayout.PropertyField(_moveEnabled);
                EditorGUILayout.PropertyField(_navigationSettings, true);
                EditorGUILayout.PropertyField(_overrideTarget);
                EditorGUILayout.PropertyField(_head);
                EditorGUILayout.PropertyField(_general, true);
                EditorGUILayout.PropertyField(_aimSpeed);
                EditorGUILayout.Space();
            }

            // Sensors
            _showSensors = GUILayout.Toggle(_showSensors, "Sensors", toolbarStyle);
            if (_showSensors)
            {
                EditorGUILayout.PropertyField(_fieldOfView, true);
                EditorGUILayout.PropertyField(_hearSensor, true);
                EditorGUILayout.Space(20);
            }

            // Patrol Areas
            _showPatrolAreas = GUILayout.Toggle(_showPatrolAreas, "Patrol Areas", toolbarStyle);
            if (_showPatrolAreas)
            {
                EditorGUILayout.PropertyField(_patrolPath);
                EditorGUILayout.PropertyField(_patrolArea);
                EditorGUILayout.Space(20);
            }

            // States
            _showStates = GUILayout.Toggle(_showStates, "States", toolbarStyle);
            if (_showStates)
            {
                EditorGUILayout.PropertyField(_patrolRandomlyIfNotHavePath);
                EditorGUILayout.PropertyField(_patrolOnHearSoundBehavior);
                EditorGUILayout.PropertyField(_moveRandom, true);
                EditorGUILayout.PropertyField(_followPatrolPath, true);
                EditorGUILayout.PropertyField(_moveRandomPatrolArea, true);
                EditorGUILayout.PropertyField(_moveToPossibleTargetPosition, true);
                EditorGUILayout.PropertyField(_searchLosedTarget, true);
                EditorGUILayout.PropertyField(_damageDetector, true);
                EditorGUILayout.PropertyField(_attack, true);
                EditorGUILayout.PropertyField(_escapeAreas, true);
                EditorGUILayout.Space();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}