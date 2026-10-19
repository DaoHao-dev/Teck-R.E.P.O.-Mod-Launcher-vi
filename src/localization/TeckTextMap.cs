using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Teck.Vietnamese {
    public static class TextMap {
        public static bool Vietnamese = true;
        public static bool IsLanguageButton(string text){return text=="切换中文"||text=="切换英文"||text=="Switch to English"||text=="Switch to Chinese"||text=="VI / EN";}
        static readonly Dictionary<string,string> map=new Dictionary<string,string>(StringComparer.Ordinal);
        static Regex phrases;
        static readonly List<Tuple<Regex,string>> templates=new List<Tuple<Regex,string>>();
        static readonly Regex tags=new Regex("(<[^>]*>)");
        public static int Count {get {return map.Count;}}
        static TextMap(){
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("teck.vi.tsv"))using(var reader=new StreamReader(stream,Encoding.UTF8)){
                string line;while((line=reader.ReadLine())!=null){int split=line.IndexOf('\t');if(split<=0)continue;string key=line.Substring(0,split).Replace("\\n","\n"),value=line.Substring(split+1).Replace("\\n","\n");map[key]=value;}
            }
            // One pass prevents a translated value being translated a second time.
            phrases=new Regex("(?<![A-Za-z])(?:"+String.Join("|",map.Keys.Where(k=>k.Length>1).OrderByDescending(k=>k.Length).Select(Regex.Escape).ToArray())+")(?![A-Za-z])",RegexOptions.CultureInvariant);
            foreach(var entry in map){
                var placeholders=Regex.Matches(entry.Key,@"\{(\d+)(?:[^}]*)\}");if(placeholders.Count==0)continue;
                var pattern=new StringBuilder("^");int previous=0;
                foreach(Match part in placeholders){pattern.Append(Regex.Escape(entry.Key.Substring(previous,part.Index-previous)));pattern.Append("(?<v"+part.Groups[1].Value+">.*?)");previous=part.Index+part.Length;}
                pattern.Append(Regex.Escape(entry.Key.Substring(previous)));pattern.Append("$");templates.Add(Tuple.Create(new Regex(pattern.ToString(),RegexOptions.Singleline),entry.Value));
            }
        }
        public static string Translate(string text){
            if(String.IsNullOrEmpty(text))return text;
            if(IsLanguageButton(text))return "VI / EN";
            if(!Vietnamese)return text;
            string direct;if(map.TryGetValue(text,out direct))return direct;
            foreach(var template in templates){var match=template.Item1.Match(text);if(match.Success)return Regex.Replace(template.Item2,@"\{(\d+)(?:[^}]*)\}",p=>match.Groups["v"+p.Groups[1].Value].Value);}
            string trimmed=text.Trim();if(map.TryGetValue(trimmed,out direct))return text.Substring(0,text.Length-text.TrimStart().Length)+direct+text.Substring(text.TrimEnd().Length);
            if(text.IndexOf('<')<0)return phrases.Replace(text,m=>map[m.Value]);
            var pieces=tags.Split(text);for(int i=0;i<pieces.Length;i++)if(!pieces[i].StartsWith("<"))pieces[i]=phrases.Replace(pieces[i],m=>map[m.Value]);
            return String.Concat(pieces);
        }
    }
}
