using UnityEngine;
using System.Collections.Generic;

namespace DebugStuff
{
    public class ConsoleToGUI : MonoBehaviour
    {
        [SerializeField] private bool showLogOnStart = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.Space;
        [SerializeField] private int maxLogEntries = 100;
        [SerializeField] private bool autoScroll = true;

        private List<string> logs = new List<string>();
        private Vector2 scrollPosition = Vector2.zero;
        private bool isShowing = true;
        private string fullLog = "";
        private bool needsScrollUpdate = false;

        void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        void Start()
        {
            isShowing = showLogOnStart;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                isShowing = !isShowing;
            }
        }

        public void HandleLog(string logString, string stackTrace, LogType type)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string newLog = "[" + timestamp + "] " + logString;

            logs.Add(newLog);

            if (logs.Count > maxLogEntries)
            {
                logs.RemoveAt(0);
            }

            // Reconstruir log completo
            fullLog = string.Join("\n", logs.ToArray());

            // Marcar que precisa atualizar o scroll para a última posição
            if (autoScroll)
            {
                needsScrollUpdate = true;
            }
        }

        void OnGUI()
        {
            if (!isShowing) return;

            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));

            // Botões de controle
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", GUILayout.Width(100)))
            {
                logs.Clear();
                fullLog = "";
                scrollPosition = Vector2.zero;
            }
            if (GUILayout.Button(isShowing ? "Hide" : "Show", GUILayout.Width(100)))
            {
                isShowing = !isShowing;
            }

            // Toggle para auto scroll
            bool newAutoScroll = GUILayout.Toggle(autoScroll, "Auto Scroll", GUILayout.Width(100));
            if (newAutoScroll != autoScroll)
            {
                autoScroll = newAutoScroll;
                if (autoScroll)
                {
                    needsScrollUpdate = true;
                }
            }

            GUILayout.EndHorizontal();

            // Área de log com scroll
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Usar um label em vez de TextArea para melhor performance
            GUILayout.Label(fullLog, GUILayout.ExpandHeight(true));

            // Se precisa atualizar o scroll, forçar para a posição máxima
            if (needsScrollUpdate && autoScroll)
            {
                // Forçar o scroll para baixo (valor grande o suficiente)
                scrollPosition = new Vector2(0, Mathf.Infinity);
                needsScrollUpdate = false;
            }

            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        public void ClearLogs()
        {
            logs.Clear();
            fullLog = "";
            scrollPosition = Vector2.zero;
        }

        // Método para adicionar log manualmente
        public void AddCustomLog(string message)
        {
            HandleLog(message, "", LogType.Log);
        }
    }
}