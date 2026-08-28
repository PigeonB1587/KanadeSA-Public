using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace KanadeSA.PreviewScene.Editor
{
    [CustomEditor(typeof(SelectableListController))]
    public class SelectableListControllerEditor : UnityEditor.Editor
    {
        private SelectableListController controller;
        private bool showSelectedEvents = true;
        private bool showDeletedEvents = true;

        private void OnEnable()
        {
            controller = target as SelectableListController;
        }

        public override void OnInspectorGUI()
        {
            // 绘制默认 Inspector（包括序列化字段）
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Subscriptions (Read-Only)", EditorStyles.boldLabel);

            // 绘制 OnItemSelected 事件订阅
            DrawEventSubscription("OnItemSelected", ref showSelectedEvents);

            // 绘制 OnItemDeleted 事件订阅
            DrawEventSubscription("OnItemDeleted", ref showDeletedEvents);
        }

        private void DrawEventSubscription(string eventName, ref bool foldout)
        {
            Delegate eventDelegate = GetEventDelegate(eventName);
            foldout = EditorGUILayout.Foldout(foldout, $"{eventName} (Subscribers: {GetSubscriberCount(eventDelegate)})", true);

            if (!foldout)
                return;

            EditorGUI.indentLevel++;

            if (eventDelegate == null)
            {
                EditorGUILayout.LabelField("No subscribers.", EditorStyles.miniLabel);
            }
            else
            {
                Delegate[] invocationList = eventDelegate.GetInvocationList();
                if (invocationList.Length == 0)
                {
                    EditorGUILayout.LabelField("No subscribers.", EditorStyles.miniLabel);
                }
                else
                {
                    // 模拟 UnityEvent 的列表样式
                    foreach (var del in invocationList)
                    {
                        EditorGUILayout.BeginHorizontal();

                        // 显示目标对象（如果是静态方法，显示 "(static)"）
                        object target = del.Target;
                        if (target != null)
                        {
                            // 显示目标对象（只读）
                            EditorGUILayout.ObjectField(GUIContent.none, target as UnityEngine.Object, typeof(UnityEngine.Object), true);
                        }
                        else
                        {
                            EditorGUILayout.LabelField("(static)", GUILayout.Width(60));
                        }

                        // 显示方法名
                        string methodName = del.Method.Name;
                        EditorGUILayout.LabelField(methodName, EditorStyles.miniLabel);

                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 通过反射获取事件的私有委托字段
        /// </summary>
        private Delegate GetEventDelegate(string eventName)
        {
            if (controller == null)
                return null;

            // 尝试查找与事件同名的私有字段（编译器生成的委托字段）
            FieldInfo field = typeof(SelectableListController).GetField(eventName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && typeof(Delegate).IsAssignableFrom(field.FieldType))
                return field.GetValue(controller) as Delegate;

            // 如果未找到，尝试查找自动属性的 backing field（若事件是自动实现的）
            string backingField = $"<{eventName}>k__BackingField";
            FieldInfo backing = typeof(SelectableListController).GetField(backingField,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (backing != null && typeof(Delegate).IsAssignableFrom(backing.FieldType))
                return backing.GetValue(controller) as Delegate;

            // 最终尝试查找所有字段，以防万一
            var allFields = typeof(SelectableListController).GetFields(
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (var f in allFields)
            {
                if (f.Name == eventName && typeof(Delegate).IsAssignableFrom(f.FieldType))
                    return f.GetValue(controller) as Delegate;
            }

            return null;
        }

        private int GetSubscriberCount(Delegate del)
        {
            if (del == null) return 0;
            return del.GetInvocationList().Length;
        }
    }
}