#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using GameFramework;
using OfficeOpenXml;
using GameFramework.Editor.DataTableTools;

//using PathCreation;
using System;

public partial class MyGameTools : EditorWindow
{
    const string DISABLE_HYBRIDCLR = "DISABLE_HYBRIDCLR";
    private static Font toFont;
    private static TMP_FontAsset tmFont;
    private static TMP_SpriteAsset fontSpAsset;

    [MenuItem("Game Framework/GameTools/Tools Window", false, 1000)]
    public static void ShowWin()
    {
        EditorWindow.GetWindow<MyGameTools>().Show();
    }
    private void OnGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.Space(10);
        toFont = (Font)EditorGUILayout.ObjectField(new GUIContent("Font:"), toFont, typeof(Font), true, GUILayout.MinWidth(100f));
        tmFont = (TMP_FontAsset)EditorGUILayout.ObjectField(new GUIContent("TMP_FontAsset:"), tmFont, typeof(TMP_FontAsset), true, GUILayout.MinWidth(100f));
        fontSpAsset = (TMP_SpriteAsset)EditorGUILayout.ObjectField(new GUIContent("TMP_SpriteAsset:"), fontSpAsset, typeof(TMP_SpriteAsset), true, GUILayout.MinWidth(100f));
        GUILayout.Space(10);
        if (GUILayout.Button("Replace Font"))
        {
            ReplaceFont();
        }
        GUILayout.Space(30);
        GUILayout.EndVertical();
    }
    [MenuItem("Game Framework/GameTools/Clear Missing Scripts【清除Prefab丢失脚本】")]
    public static void ClearMissingScripts()
    {
        var pfbArr = AssetDatabase.FindAssets("t:Prefab");
        foreach (var item in pfbArr)
        {
            var pfbFileName = AssetDatabase.GUIDToAssetPath(item);
            var pfb = AssetDatabase.LoadAssetAtPath<GameObject>(pfbFileName);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(pfb);
        }
    }
    [MenuItem("Game Framework/GameTools/Refresh All Excels【刷新配置表】", false, 1001)]
    public static async void GenerateDataTables()
    {
        var appConfig = await AppConfigs.GetInstanceSync();
        RefreshAllDataTable(appConfig.DataTables);
        RefreshAllConfig(appConfig.Configs);

        try
        {
            GenerateUIViewScript();
        }
        catch (System.Exception e)
        {
            Debug.LogErrorFormat("生成UIView.cs失败:{0}", e.Message);
            throw;
        }
        GenerateGroupEnumScript();
        AssetDatabase.Refresh();
    }
    /// <summary>
    /// 生成Entity,Sound,UI枚举脚本
    /// </summary>
    public static void GenerateGroupEnumScript()
    {
        DirectoryInfo assetDir = new DirectoryInfo(Application.dataPath);
        var excelDir = UtilityBuiltin.ResPath.GetCombinePath(assetDir.Parent.FullName, "DataTables");
        if (!Directory.Exists(excelDir))
        {
            Debug.LogErrorFormat("Excel DataTable directory is not exists:{0}", excelDir);
            return;
        }
        string[] groupExcels = { ConstEditor.EntityGroupTableExcel, ConstEditor.UIGroupTableExcel, ConstEditor.SoundGroupTableExcel };
        StringBuilder sBuilder = new StringBuilder();
        sBuilder.AppendLine("public static partial class Const");
        sBuilder.AppendLine("{");
        foreach (var excel in groupExcels)
        {
            var excelFileName = UtilityBuiltin.ResPath.GetCombinePath(excelDir, excel);
            var excelFileInfo = new FileInfo(excelFileName);
            if (!excelFileInfo.Exists)
            {
                Debug.LogErrorFormat("Excel is not exists:{0}", excelFileName);
                return;
            }
            var excelPackage = new ExcelPackage(excelFileInfo);
            var excelSheet = excelPackage.Workbook.Worksheets[0];
            List<string> groupList = new List<string>();
            for (int rowIndex = excelSheet.Dimension.Start.Row; rowIndex <= excelSheet.Dimension.End.Row; rowIndex++)
            {
                var rowStr = excelSheet.GetValue(rowIndex, 1);
                if (rowStr != null && rowStr.ToString().StartsWith("#"))
                {
                    continue;
                }
                var groupName = excelSheet.GetValue(rowIndex, 4).ToString();
                if (!groupList.Contains(groupName)) groupList.Add(groupName);
            }
            excelSheet.Dispose();
            excelPackage.Dispose();
            var className = excelFileInfo.Name.Substring(0, excelFileInfo.Name.Length - "Table".Length - excelFileInfo.Extension.Length);

            sBuilder.AppendLine(Utility.Text.Format("\tpublic enum {0}", className));
            sBuilder.AppendLine("\t{");
            for (int i = 0; i < groupList.Count; i++)
            {
                if (i < groupList.Count - 1)
                {
                    sBuilder.AppendLine(Utility.Text.Format("\t\t{0},", groupList[i]));
                }
                else
                {
                    sBuilder.AppendLine(Utility.Text.Format("\t\t{0}", groupList[i]));
                }
            }
            sBuilder.AppendLine("\t}");
        }
        sBuilder.AppendLine("}");

        var outFileName = ConstEditor.ConstGroupScriptFileFullName;
        try
        {
            File.WriteAllText(outFileName, sBuilder.ToString());
            Debug.LogFormat("------------------成功生成Group文件:{0}---------------", outFileName);
        }
        catch (Exception e)
        {
            Debug.LogErrorFormat("Group文件生成失败:{0}", e.Message);
            throw;
        }
    }
    /// <summary>
    /// 生成UI界面枚举类型
    /// </summary>
    public static void GenerateUIViewScript()
    {
        DirectoryInfo assetDir = new DirectoryInfo(Application.dataPath);
        var excelDir = UtilityBuiltin.ResPath.GetCombinePath(assetDir.Parent.FullName, "DataTables");
        if (!Directory.Exists(excelDir))
        {
            return;
        }
        var excelFileName = UtilityBuiltin.ResPath.GetCombinePath(excelDir, ConstEditor.UITableExcel);
        var excelFileInfo = new FileInfo(excelFileName);
        if (!excelFileInfo.Exists)
        {
            return;
        }
        var excelPackage = new ExcelPackage(excelFileInfo);
        var excelSheet = excelPackage.Workbook.Worksheets[0];
        Dictionary<int, string> uiViewDic = new Dictionary<int, string>();
        for (int rowIndex = excelSheet.Dimension.Start.Row; rowIndex <= excelSheet.Dimension.End.Row; rowIndex++)
        {
            var rowStr = excelSheet.GetValue(rowIndex, 1);
            if (rowStr != null && rowStr.ToString().StartsWith("#"))
            {
                continue;
            }
            uiViewDic.Add(int.Parse(excelSheet.GetValue(rowIndex, 2).ToString()), excelSheet.GetValue(rowIndex, 5).ToString());
        }
        excelSheet.Dispose();
        excelPackage.Dispose();
        StringBuilder sBuilder = new StringBuilder();
        sBuilder.AppendLine("public enum UIViews : int");
        sBuilder.AppendLine("{");
        int curIndex = 0;
        foreach (KeyValuePair<int, string> uiItem in uiViewDic)
        {
            if (curIndex < uiViewDic.Count - 1)
            {
                sBuilder.AppendLine(Utility.Text.Format("\t{0} = {1},", uiItem.Value, uiItem.Key));
            }
            else
            {
                sBuilder.AppendLine(Utility.Text.Format("\t{0} = {1}", uiItem.Value, uiItem.Key));
            }
            curIndex++;
        }
        sBuilder.AppendLine("}");
        File.WriteAllText(ConstEditor.UIViewScriptFile, sBuilder.ToString());
        Debug.LogFormat("-------------------成功生成UIViews.cs-----------------");
    }
    /// <summary>
    /// 查找UI上的文字用于语言国际化
    /// </summary>
    [MenuItem("Game Framework/GameTools/Find Localization String【生成多语言】", false, 1002)]
    public static void FindLocalizationString()
    {
        EditorUtility.DisplayProgressBar("Progress", "Find Localization String...", 0);
        string[] dirs = { "Assets/AAAGame/Prefabs/UI" };
        var asstIds = AssetDatabase.FindAssets("t:Prefab", dirs);
        int count = 0;
        List<string> str_list = new List<string>();
        for (int i = 0; i < asstIds.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(asstIds[i]);
            var pfb = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Text item in pfb.GetComponentsInChildren<Text>(true))
            {
                var str = item.text;
                //str.Replace(@"\n", @"\\n");
                str = Regex.Replace(str, @"\n", @"\n");

                if (string.IsNullOrWhiteSpace(str))
                {
                    continue;
                }
                if (!str_list.Contains(str))
                {
                    str_list.Add(str);
                }
            }
            count++;
            EditorUtility.DisplayProgressBar("Find Class", pfb.name, count / (float)asstIds.Length);
        }
        string content = string.Empty;
        foreach (var item in str_list)
        {
            content += string.Format("\"{0}\":\"{0}\",\n", item);
        }
        System.IO.File.WriteAllText(Application.dataPath + "/Localization.txt", content);
        EditorUtility.ClearProgressBar();
    }

    /// <summary>
    /// 批量替换字体文件
    /// </summary>
    public static void ReplaceFont()
    {
        EditorUtility.DisplayProgressBar("Progress", "Replace Font...", 0);
        var asstIds = AssetDatabase.FindAssets("t:Prefab", ConstEditor.PrefabsPath);
        int count = 0;
        for (int i = 0; i < asstIds.Length; i++)
        {
            bool isChanged = false;
            string path = AssetDatabase.GUIDToAssetPath(asstIds[i]);
            var pfb = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            //var pfb = PrefabUtility.InstantiatePrefab(pfbFile) as GameObject;//不涉及增删节点,不用实例化
            if (toFont != null)
            {
                var texts = pfb.GetComponentsInChildren<Text>(true);
                foreach (var item in texts)
                {
                    item.font = toFont;
                }
                isChanged = texts.Length > 0;
            }
            if (fontSpAsset != null)
            {
                var tmTexts = pfb.GetComponentsInChildren<TextMeshPro>(true);
                foreach (var item in tmTexts)
                {
                    //item.font = tmFont;
                    if (item.spriteAsset != null && item.spriteAsset.name == "OtherIcons")
                    {
                        item.spriteAsset = fontSpAsset;
                        isChanged = true;
                    }

                }
            }

            if (isChanged)
            {
                PrefabUtility.SavePrefabAsset(pfb, out bool success);
                if (success)
                {
                    count++;
                }
            }
            else
            {
                count++;
            }

            EditorUtility.DisplayProgressBar("Replace Font Progress", pfb.name, count / (float)asstIds.Length);
        }
        EditorUtility.ClearProgressBar();
    }

    #region 通用方法
    public static void FindChildByName(Transform root, string name, ref Transform result)
    {
        if (root.name.StartsWith(name))
        {
            result = root;
            return;
        }

        foreach (Transform child in root)
        {
            FindChildByName(child, name, ref result);
        }
    }
    public static void FindChildrenByName(Transform root, string name, ref List<Transform> result)
    {
        if (root.name.StartsWith(name))
        {
            result.Add(root);

        }

        foreach (Transform child in root)
        {
            FindChildrenByName(child, name, ref result);
        }
    }
    public static string GetNodePath(Transform node, Transform root = null)
    {
        if (node == null)
        {
            return string.Empty;
        }
        Transform curNode = node;
        string path = curNode.name;
        while (curNode.parent != root)
        {
            curNode = curNode.parent;
            path = string.Format("{0}/{1}", curNode.name, path);
        }
        return path;
    }


    /// <summary>
    /// Excel转换为Txt
    /// </summary>
    public static bool Excel2TxtFile(string excelFileName, string outTxtFile)
    {
        bool result = false;
        var fileInfo = new FileInfo(excelFileName);
        string tmpExcelFile;

        tmpExcelFile = UtilityBuiltin.ResPath.GetCombinePath(fileInfo.Directory.FullName, Utility.Text.Format("{0}.temp", fileInfo.Name));
        File.Copy(excelFileName, tmpExcelFile, true);

        var excelFileInfo = new FileInfo(tmpExcelFile);
        using (var excelPackage = new ExcelPackage(excelFileInfo))
        {
            var excelSheet = excelPackage.Workbook.Worksheets[0];
            string excelTxt = string.Empty;
            for (int rowIndex = excelSheet.Dimension.Start.Row; rowIndex <= excelSheet.Dimension.End.Row; rowIndex++)
            {
                string rowTxt = string.Empty;
                for (int colIndex = excelSheet.Dimension.Start.Column; colIndex <= excelSheet.Dimension.End.Column; colIndex++)
                {
                    rowTxt = Utility.Text.Format("{0}{1}\t", rowTxt, excelSheet.GetValue(rowIndex, colIndex));
                }
                rowTxt = rowTxt.Substring(0, rowTxt.Length - 1);
                excelTxt = Utility.Text.Format("{0}{1}\n", excelTxt, rowTxt);
            }
            excelTxt = excelTxt.TrimEnd('\n');
            excelSheet.Dispose();
            excelPackage.Dispose();
            try
            {
                File.WriteAllText(outTxtFile, excelTxt, Encoding.UTF8);
                result = true;
            }
            catch (Exception e)
            {
                throw e;
            }
            
        }
        if (File.Exists(tmpExcelFile))
        {
            File.Delete(tmpExcelFile);
        }
        return result;
    }
    //[MenuItem("Game Framework/GameTools/Refresh All GameConfigs")]
    public static void RefreshAllConfig(string[] files = null)
    {
        var configDir = ConstEditor.ConfigExcelPath;
        if (!Directory.Exists(configDir))
        {
            return;
        }
        string[] excelFiles;
        if (files == null)
        {
            excelFiles = Directory.GetFiles(configDir, "*.xlsx", SearchOption.TopDirectoryOnly);
        }
        else
        {
            excelFiles = GetABTestExcelFiles(configDir, files);
        }
        for (int i = 0; i < excelFiles.Length; i++)
        {
            var excelFileName = excelFiles[i];
            string savePath = UtilityBuiltin.ResPath.GetCombinePath(ConstEditor.GameConfigPath, Utility.Text.Format("{0}.txt", Path.GetFileNameWithoutExtension(excelFileName)));
            try
            {
                if(Excel2TxtFile(excelFileName, savePath))
                {
                    Debug.LogFormat("------------导出Config表成功:{0}", savePath);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat("Config数据表转txt失败:{0}", e.Message);
                throw;
            }
        }
        AssetDatabase.Refresh();
    }
    public static async void RefreshAllDataTable(string[] files = null)
    {
        var excelDir = ConstEditor.DataTableExcelPath;
        if (!Directory.Exists(excelDir))
        {
            Debug.LogWarningFormat("Excel数据表文件夹不存在:{0}", excelDir);
            return;
        }

        string[] excelFiles;
        if (files == null)
        {
            excelFiles = Directory.GetFiles(excelDir, "*.xlsx", SearchOption.TopDirectoryOnly);
        }
        else
        {
            excelFiles = GetABTestExcelFiles(excelDir, files);
        }
        for (int i = 0; i < excelFiles.Length; i++)
        {
            var excelFileName = excelFiles[i];
            var fileName = Path.GetFileNameWithoutExtension(excelFileName);
            string savePath = UtilityBuiltin.ResPath.GetCombinePath(ConstEditor.DataTablePath, Utility.Text.Format("{0}.txt", fileName));

            try
            {
                Excel2TxtFile(excelFileName, savePath);
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat("Excel数据表转txt失败:{0}", e.Message);
                throw;
            }

        }

        //生成数据表代码
        var appConfig = await AppConfigs.GetInstanceSync();
        foreach (var dataTableName in appConfig.DataTables)
        {
            string tbTxtFile = Utility.Path.GetRegularPath(Path.Combine(ConstEditor.DataTablePath, dataTableName + ".txt"));
            if (!File.Exists(tbTxtFile))
            {
                continue;
            }
            var dataTable = dataTableName;
            DataTableProcessor dataTableProcessor = DataTableGenerator.CreateDataTableProcessor(dataTable);
            if (!DataTableGenerator.CheckRawData(dataTableProcessor, dataTable))
            {
                Debug.LogError(Utility.Text.Format("Check raw data failure. DataTableName='{0}'", dataTable));
                break;
            }

            DataTableGenerator.GenerateCodeFile(dataTableProcessor, dataTable);
        }
        AssetDatabase.Refresh();
    }
    private static string[] GetABTestExcelFiles(string excelDir,string[] files)
    {
        string[] excelFiles = Directory.GetFiles(excelDir, "*.xlsx", SearchOption.TopDirectoryOnly);
        string[] result = new string[0];
        foreach (var rawName in files)
        {
            string abFileHeader = rawName + "_";
            foreach (var excelFile in excelFiles)
            {
                string excelName = Path.GetFileNameWithoutExtension(excelFile);
                bool isABFile = excelName.CompareTo(rawName) == 0 || excelName.StartsWith(abFileHeader);
                if (isABFile) ArrayUtility.Add(ref result, excelFile);
            }
        }
        return result;
    }
    private static UnityEditor.BuildTargetGroup GetCurrentBuildTarget()
    {
#if UNITY_ANDROID
        return UnityEditor.BuildTargetGroup.Android;
#elif UNITY_IOS
        return UnityEditor.BuildTargetGroup.iOS;
#elif UNITY_STANDALONE
        return UnityEditor.BuildTargetGroup.Standalone;
#elif UNITY_WEBGL
        return UnityEditor.BuildTargetGroup.WebGL;
#else
        return UnityEditor.BuildTargetGroup.Unknown;
#endif
    }
#if UNITY_2021_1_OR_NEWER
    private static UnityEditor.Build.NamedBuildTarget GetCurrentNamedBuildTarget()
    {
#if UNITY_ANDROID
        return UnityEditor.Build.NamedBuildTarget.Android;
#elif UNITY_IOS
        return UnityEditor.Build.NamedBuildTarget.iOS;
#elif UNITY_STANDALONE
        return UnityEditor.Build.NamedBuildTarget.Standalone;
#elif UNITY_WEBGL
        return UnityEditor.Build.NamedBuildTarget.WebGL;
#else
        return UnityEditor.Build.NamedBuildTarget.Unknown;
#endif
    }
#endif
    #endregion
}
#endif