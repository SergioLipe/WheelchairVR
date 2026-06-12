// =============================================================
//  TextExtractor.cs
//  Coloca este ficheiro numa pasta chamada "Editor"
//  (ex: Assets/_Project/Scripts/Editor/TextExtractor.cs)
//
//  COMO USAR:
//  1. Mete o ficheiro numa pasta "Editor"
//  2. No menu do Unity: Tools -> Localization -> Extrair Textos das Cenas
//  3. Gera um ficheiro "textos_para_traduzir.csv" na raiz do projeto
//  4. Abre o CSV no Excel/Google Sheets para preencher as traducoes
// =============================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class TextExtractor
{
    [MenuItem("Tools/Localization/Extrair Textos das Cenas")]
    public static void ExtractAllText()
    {
        // Lista para guardar tudo o que encontrarmos
        // Cada linha: cena | caminho do objeto | texto
        List<string[]> rows = new List<string[]>();
        HashSet<string> uniqueTexts = new HashSet<string>();

        // Guardar a cena atual para a repor no fim
        string currentScenePath = EditorSceneManager.GetActiveScene().path;

        // Percorrer todas as cenas que estao no Build Settings
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;

        if (buildScenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Sem cenas",
                "Nao ha cenas no Build Settings. Adiciona as tuas cenas em File > Build Settings.",
                "OK");
            return;
        }

        int sceneCount = 0;
        foreach (var bScene in buildScenes)
        {
            if (!bScene.enabled) continue;
            if (string.IsNullOrEmpty(bScene.path)) continue;

            sceneCount++;
            // Abrir a cena
            Scene scene = EditorSceneManager.OpenScene(bScene.path, OpenSceneMode.Single);
            string sceneName = scene.name;

            // Encontrar TODOS os TextMeshPro (UI e world-space), incluindo inativos
            // Usamos Resources.FindObjectsOfTypeAll para apanhar tambem os inativos
            TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();

            foreach (TMP_Text tmp in allTexts)
            {
                // Ignorar prefabs/assets que nao estao na cena
                if (tmp == null) continue;
                if (tmp.gameObject.scene != scene) continue;

                string txt = tmp.text;
                // Ignorar vazios ou so espacos
                if (string.IsNullOrWhiteSpace(txt)) continue;

                // Limpar quebras de linha para o CSV nao partir
                string cleanTxt = txt.Replace("\n", " ").Replace("\r", " ").Trim();

                string objPath = GetGameObjectPath(tmp.transform);
                rows.Add(new string[] { sceneName, objPath, cleanTxt });
                uniqueTexts.Add(cleanTxt);
            }
        }

        // Repor a cena original
        if (!string.IsNullOrEmpty(currentScenePath))
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);

        // ---- Escrever o CSV completo (todas as ocorrencias) ----
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Cena;Objeto;Texto Original (PT);Traducao (EN);Key sugerida");
        foreach (var row in rows)
        {
            string keySuggestion = SuggestKey(row[2]);
            sb.AppendLine($"{Escape(row[0])};{Escape(row[1])};{Escape(row[2])};;{keySuggestion}");
        }

        string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                       "textos_para_traduzir.csv");
        File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(true)); // BOM para acentos no Excel

        // ---- Escrever tambem uma lista de textos UNICOS (sem repetidos) ----
        StringBuilder sbUnique = new StringBuilder();
        sbUnique.AppendLine("Texto Original (PT);Traducao (EN);Key sugerida");
        foreach (var t in uniqueTexts)
        {
            sbUnique.AppendLine($"{Escape(t)};;{SuggestKey(t)}");
        }
        string uniquePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                         "textos_unicos.csv");
        File.WriteAllText(uniquePath, sbUnique.ToString(), new UTF8Encoding(true));

        AssetDatabase.Refresh();

        Debug.Log($"<color=green>[TextExtractor]</color> Concluido! " +
                  $"{sceneCount} cenas, {rows.Count} textos no total, " +
                  $"{uniqueTexts.Count} textos unicos.");
        Debug.Log($"<color=cyan>Ficheiros gerados na raiz do projeto:</color>\n" +
                  $"  - textos_para_traduzir.csv (todas as ocorrencias)\n" +
                  $"  - textos_unicos.csv (sem repetidos - usa este para a tabela)");

        EditorUtility.DisplayDialog("Extracao concluida",
            $"Encontrados {rows.Count} textos ({uniqueTexts.Count} unicos) em {sceneCount} cenas.\n\n" +
            "Ficheiros gerados na raiz do projeto:\n" +
            "- textos_para_traduzir.csv\n" +
            "- textos_unicos.csv\n\n" +
            "Abre o textos_unicos.csv no Excel para traduzir.",
            "Fixe!");
    }

    // Constroi o caminho completo do objeto na hierarquia (ex: Canvas/Panel/Botao)
    private static string GetGameObjectPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    // Sugere uma Key a partir do texto (minusculas, sem acentos, underscores)
    private static string SuggestKey(string text)
    {
        string s = text.ToLowerInvariant();
        // remover acentos comuns PT
        s = s.Replace("á","a").Replace("à","a").Replace("ã","a").Replace("â","a")
             .Replace("é","e").Replace("ê","e").Replace("í","i")
             .Replace("ó","o").Replace("õ","o").Replace("ô","o")
             .Replace("ú","u").Replace("ç","c");
        // so letras e numeros, resto vira underscore
        StringBuilder sb = new StringBuilder();
        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_') sb.Append('_');
        }
        string key = sb.ToString();
        // limitar tamanho e limpar underscores repetidos
        while (key.Contains("__")) key = key.Replace("__", "_");
        key = key.Trim('_');
        if (key.Length > 30) key = key.Substring(0, 30).Trim('_');
        return key;
    }

    // Escapar para CSV (aspas e ponto-e-virgula)
    private static string Escape(string s)
    {
        if (s.Contains("\"") || s.Contains(";"))
        {
            s = s.Replace("\"", "\"\"");
            return $"\"{s}\"";
        }
        return s;
    }
}