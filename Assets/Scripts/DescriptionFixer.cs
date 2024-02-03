using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

public class DescriptionFixer : MonoBehaviour
{
    private static bool s_enabled, s_hasPatched;
    private static DescriptionFixer s_instance;
    private static Func<string, string[]> s_getLanguagesField;

    private void GenerateSetter()
    {
        // term => Localization.modLanguageSourceData.GetTermData(term, false)?.Languages

        Type localizationType = Type
            .GetType("Localization, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
        Type languageSourceDataType = Type
            .GetType("I2.Loc.LanguageSourceData, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
        Type termDataType = Type
            .GetType("I2.Loc.TermData, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");

        var termParam = Expression.Parameter(typeof(string), "term");
        var modLanguageSourceData = Expression.Field(
                null,
                localizationType.GetField("modLanguageSourceData", BindingFlags.NonPublic | BindingFlags.Static)
            );
        var termData = Expression.Call(
                modLanguageSourceData,
                languageSourceDataType.GetMethod("GetTermData", BindingFlags.Public | BindingFlags.Instance),
                termParam,
                Expression.Constant(false, typeof(bool))
            );
        var isTermDataNull = Expression.Equal(termData, Expression.Constant(null, termDataType));
        var languages = Expression.Field(
                termData,
                termDataType.GetField("Languages", BindingFlags.Public | BindingFlags.Instance)
            );
        var returnArray = Expression.Condition(isTermDataNull, Expression.Constant(null, typeof(string[])), languages);

        s_getLanguagesField = Expression.Lambda<Func<string, string[]>>(returnArray, termParam).Compile();
    }

    private void Patch()
    {
        Harmony harmony = new Harmony("BDB.DMGDescriptionFixer");
        MethodInfo intercepted = Type
            .GetType("Localization, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
            .GetMethod("AddModTermData", BindingFlags.Public | BindingFlags.Static);
        harmony.Patch(intercepted, postfix: new HarmonyMethod(typeof(DescriptionFixer), "SetTermPostfix"));
    }

    private static void SetTermPostfix(string term, string englishString)
    {
        if (!s_enabled)
            return;

        var arr = s_getLanguagesField(term);
        if (arr != null)
            arr[0] = englishString;
    }

    #region Unity Life Cycle
    private void Awake()
    {
        if (s_instance != null)
        {
            Debug.Log("[DMG Description Fixer] Duplicate instance! Discarding.");
            Destroy(gameObject);
            return;
        }
        s_instance = this;
        s_enabled = true;

        if (!s_hasPatched)
        {
            GenerateSetter();
            Patch();
            s_hasPatched = true;
        }
    }
    private void OnDestroy()
    {
        s_instance = null;
        s_enabled = false;
    }
    private void OnEnable()
    {
        s_enabled = true;
    }
    private void OnDisable()
    {
        s_enabled = false;
    }
    #endregion
}
