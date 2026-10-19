using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;
namespace Teck.Vietnamese {
    public static class UIBridge {
        static Font vietnameseFont;
        static TMPro.TMP_FontAsset fallback;
        static readonly HashSet<int> preparedFonts=new HashSet<int>();
        static Font GetFont(){if(vietnameseFont==null)vietnameseFont=Font.CreateDynamicFontFromOSFont("Arial",18);return vietnameseFont;}
        public static string Text(string value){return TextMap.Translate(value);}
        public static Action<bool> SaveLanguage;
        public static bool LanguageButton(bool clicked,string label){
            if(!TextMap.IsLanguageButton(label))return clicked;
            if(clicked){TextMap.Vietnamese=!TextMap.Vietnamese;if(SaveLanguage!=null)SaveLanguage(TextMap.Vietnamese);}
            return false;
        }
        public static string[] Texts(string[] values){return values==null?null:values.Select(Text).ToArray();}
        public static GUIContent Content(GUIContent value){return value==null?null:new GUIContent(value){text=Text(value.text),tooltip=Text(value.tooltip)};}
        public static GUIContent[] Contents(GUIContent[] values){return values==null?null:values.Select(Content).ToArray();}
        public static GUIStyle Style(GUIStyle original){
            if(original==null)return null;
            try{if(original.font==null||!original.font.HasCharacter('ệ')||!original.font.HasCharacter('Đ'))return new GUIStyle(original){font=GetFont()};}catch{}
            return original;
        }
        public static void PrepareTMP(object instance){
            try{
                var text=instance as TMPro.TMP_Text;if(text==null||text.font==null)return;
                var primary=text.font;int id=primary.GetInstanceID();if(preparedFonts.Contains(id))return;
                if(primary.HasCharacter('ệ',true,true)&&primary.HasCharacter('Đ',true,true)){preparedFonts.Add(id);return;}
                if(fallback==null){fallback=TMPro.TMP_FontAsset.CreateFontAsset(GetFont());UnityEngine.Object.DontDestroyOnLoad(fallback);}
                if(primary.fallbackFontAssetTable==null)primary.fallbackFontAssetTable=new List<TMPro.TMP_FontAsset>();
                primary.fallbackFontAssetTable.Add(fallback);preparedFonts.Add(id);
            }catch{}
        }
    }
    [BepInPlugin("teck.vietnamese.ui","Teck - Tieng Viet","1.2.0")]
    public class Plugin:BaseUnityPlugin {
        void Awake(){
            var language=Config.Bind("Language","Vietnamese",true,"Use Vietnamese; toggle VI / EN in the mod window to switch to English.");
            TextMap.Vietnamese=language.Value;UIBridge.SaveLanguage=value=>{language.Value=value;Config.Save();};
            Logger.LogInfo("Teck VI / EN: "+TextMap.Count+" translations.");
        }
    }
}
