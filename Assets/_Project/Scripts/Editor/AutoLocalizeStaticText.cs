// Coloca numa pasta "Editor" (ex: Assets/_Project/Scripts/Editor/)
// USO: abre a cena -> Tools > Localization > Auto-Ligar Textos Estaticos (Cena Atual)
// Se algo correr mal: CTRL+Z desfaz tudo.

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization.Components;
using UnityEditor.Localization;

public class AutoLocalizeStaticText
{
    private const string TABLE_NAME = "GameText";

    // Nomes de objetos cujo texto é DINÂMICO (escrito por código). Se o nome do
    // objeto OU de um pai contiver um destes (case-insensitive), SALTA.
    private static readonly string[] dynamicObjectNames = new string[]
    {
        "timeText", "collisionsText", "slidesText", "modeText", "speedText",
        "StarCounter", "Counter",
        "countdownText", "Countdown",
        "txtCurrentProfile", "CurrentProfile",
        "txtHistorySubtitle", "HistorySubtitle",
        "txtHistFreestyleCount", "FreestyleCount",
        "txtHistoryDetails", "HistoryDetails",
        "txtPatientName", "PatientName",
        "txtLevelTitle", "LevelTitle",
        "txtAttemptsCount", "AttemptsCount",
        "txtConfirmMessage", "ConfirmMessage",
        "AttemptsText", "Number",
        "Btn_LoginProfile", "Btn_SelectProfile", "InputEditName",
        "Template", "Placeholder",
    };

    [MenuItem("Tools/Localization/Auto-Ligar Textos Estaticos (Cena Atual)")]
    public static void AutoLocalize()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(TABLE_NAME);
        if (collection == null)
        {
            EditorUtility.DisplayDialog("Erro",
                $"Não encontrei a String Table '{TABLE_NAME}'. Confirma o nome.", "OK");
            return;
        }

        Dictionary<string, string> textToKey = BuildTextToKeyMap(collection);

        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();

        int ligados = 0, saltadosDinamico = 0, saltadosSemMatch = 0, jaTinham = 0;
        StringBuilder relatorio = new StringBuilder();
        relatorio.AppendLine("=== RELATÓRIO AUTO-LOCALIZE ===\n");

        foreach (TMP_Text tmp in allTexts)
        {
            if (tmp == null) continue;
            if (!tmp.gameObject.scene.IsValid()) continue;

            GameObject go = tmp.gameObject;
            string txt = tmp.text != null ? tmp.text.Trim() : "";

            if (go.GetComponent<LocalizeStringEvent>() != null) { jaTinham++; continue; }

            if (IsUnderDynamicObject(go.transform))
            {
                saltadosDinamico++;
                relatorio.AppendLine($"[DINÂMICO-NOME] {GetPath(go.transform)}  (\"{Short(txt)}\")");
                continue;
            }

            if (LooksDynamic(txt))
            {
                saltadosDinamico++;
                relatorio.AppendLine($"[DINÂMICO-TEXTO] {GetPath(go.transform)}  (\"{Short(txt)}\")");
                continue;
            }

            string key = FindKey(textToKey, txt);
            if (key == null)
            {
                saltadosSemMatch++;
                relatorio.AppendLine($"[SEM MATCH] {GetPath(go.transform)}  (\"{Short(txt)}\")");
                continue;
            }

            Undo.RecordObject(go, "Auto Localize");
            LocalizeStringEvent loc = Undo.AddComponent<LocalizeStringEvent>(go);

            var reference = new LocalizedString { TableReference = TABLE_NAME, TableEntryReference = key };
            loc.StringReference = reference;

            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                loc.OnUpdateString, new UnityEngine.Events.UnityAction<string>(tmp.SetText));

            loc.RefreshString();
            EditorUtility.SetDirty(go);
            ligados++;
            relatorio.AppendLine($"[LIGADO] {GetPath(go.transform)}  ->  '{key}'  (\"{Short(txt)}\")");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        relatorio.Insert(0,
            $"Ligados: {ligados} | Saltados dinâmicos: {saltadosDinamico} | " +
            $"Sem match: {saltadosSemMatch} | Já tinham: {jaTinham}\n\n");

        Debug.Log(relatorio.ToString());

        EditorUtility.DisplayDialog("Auto-Localize concluído",
            $"Ligados: {ligados}\nSaltados (dinâmicos): {saltadosDinamico}\n" +
            $"Saltados (sem match): {saltadosSemMatch}\nJá tinham: {jaTinham}\n\n" +
            "Vê o relatório na Console. CTRL+Z desfaz tudo.", "OK");
    }

    private static Dictionary<string, string> BuildTextToKeyMap(StringTableCollection collection)
    {
        var map = new Dictionary<string, string>();
        var sharedData = collection.SharedData;

        foreach (var table in collection.StringTables)
        {
            foreach (var entry in table.Values)
            {
                if (entry == null || string.IsNullOrEmpty(entry.LocalizedValue)) continue;
                var sharedEntry = sharedData.GetEntry(entry.KeyId);
                if (sharedEntry == null) continue;
                string keyName = sharedEntry.Key;
                string norm = Normalize(entry.LocalizedValue);
                if (!map.ContainsKey(norm)) map[norm] = keyName;
            }
        }
        return map;
    }

    private static string FindKey(Dictionary<string, string> map, string text)
    {
        string norm = Normalize(text);
        return map.TryGetValue(norm, out string key) ? key : null;
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder();
        bool inTag = false;
        foreach (char c in s)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }
        return sb.ToString().Trim().ToLowerInvariant();
    }

    private static bool IsUnderDynamicObject(Transform t)
    {
        Transform cur = t;
        while (cur != null)
        {
            string n = cur.name.ToLowerInvariant();
            foreach (string dyn in dynamicObjectNames)
                if (n.Contains(dyn.ToLowerInvariant())) return true;
            cur = cur.parent;
        }
        return false;
    }

    private static bool LooksDynamic(string txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return true;
        foreach (char c in txt) if (char.IsDigit(c)) return true;
        string low = txt.ToLowerInvariant();
        string[] junk = { "placeholder", "new text", "sample text", "enter text", "lorem" };
        foreach (string j in junk) if (low.Contains(j)) return true;
        if (txt.Contains("{")) return true;
        return false;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }

    private static string Short(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\n", " ");
        return s.Length > 40 ? s.Substring(0, 40) + "..." : s;
    }
}