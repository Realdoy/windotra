using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WinCanvas {
 // Live data for widgets, fetched in the background and cached so the desktop never waits:
 //  {claude-tokens-today} {claude-tokens-week} {claude-messages-today}  your Claude Code usage, from its own logs on this PC
 //  {web:https://example.com/data.json|path.to.value}                     a value from any JSON on the web (HTTPS only)
 //  {file:C:\path\note.txt}                                              the first line of a text file
 //  {json-file:C:\path\data.json|path.to.value}                          a value from a JSON file on this PC
 public static class WidgetData {
  static readonly Dictionary<string,string> cache=new Dictionary<string,string>();
  static readonly Dictionary<string,DateTime> fetched=new Dictionary<string,DateTime>();
  static readonly HashSet<string> busy=new HashSet<string>();
  static readonly HttpClient http=new HttpClient{Timeout=TimeSpan.FromSeconds(8)};
  static WidgetData(){http.DefaultRequestHeaders.UserAgent.ParseAdd("WinDotra-widget");}
  public static string Get(string key,string arg){
   string id=key+"|"+arg;string value;DateTime when;
   lock(cache){if(!fetched.TryGetValue(id,out when)||(DateTime.Now-when).TotalSeconds>(key.StartsWith("claude")?900:key=="web"?300:60)){if(!busy.Contains(id)){busy.Add(id);Task.Run(()=>Refresh(id,key,arg));}}if(cache.TryGetValue(id,out value))return value;}
   return "…";
  }
  static void Refresh(string id,string key,string arg){
   string value;
   try{value=Read(key,arg);}catch(Exception){value="—";}
   lock(cache){cache[id]=value;fetched[id]=DateTime.Now;busy.Remove(id);}
  }
  static string Read(string key,string arg){
   switch(key){
    case "claude-tokens-today":return Format(ClaudeUsage(DateTime.Today).Item1);
    case "claude-tokens-week":return Format(ClaudeUsage(DateTime.Today.AddDays(-6)).Item1);
    case "claude-messages-today":return ClaudeUsage(DateTime.Today).Item2.ToString();
    case "web":{var parts=Split(arg);Uri u;if(!Uri.TryCreate(parts[0],UriKind.Absolute,out u)||u.Scheme!="https")return "https only";var text=http.GetStringAsync(u).Result;if(text.Length>256*1024)return "too large";return parts[1].Length==0?text.Trim().Split('\n')[0]:Pick(text,parts[1]);}
    case "file":{if(!File.Exists(arg))return "no file";using(var r=new StreamReader(arg))return (r.ReadLine()??"").Trim();}
    case "json-file":{var parts=Split(arg);if(!File.Exists(parts[0])||new FileInfo(parts[0]).Length>1024*1024)return "no file";return Pick(File.ReadAllText(parts[0]),parts[1]);}
   }
   return "?";
  }
  static string[] Split(string arg){int bar=(arg??"").LastIndexOf('|');return bar<0?new[]{arg??"",""}:new[]{arg.Substring(0,bar).Trim(),arg.Substring(bar+1).Trim()};}
  // Follows a path like "data.items.0.price" through JSON.
  static string Pick(string json,string path){object node=new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.DeserializeObject(json);foreach(var part in path.Split('.')){if(part.Length==0)continue;var obj=node as Dictionary<string,object>;var arr=node as object[];int i;if(obj!=null){if(!obj.TryGetValue(part,out node))return "?";}else if(arr!=null&&int.TryParse(part,out i)&&i<arr.Length)node=arr[i];else return "?";}return node==null?"":Convert.ToString(node,System.Globalization.CultureInfo.InvariantCulture);}
  static string Format(long n){return n>=1000000?(n/1000000.0).ToString("0.0")+"M":n>=1000?(n/1000.0).ToString("0.0")+"k":n.ToString();}
  // Adds up input and output tokens from Claude Code's local session logs since a date.
  static Tuple<long,int> ClaudeUsage(DateTime since){
   string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","projects");long tokens=0;int messages=0;if(!Directory.Exists(root))return Tuple.Create(0L,0);
   var tokenField=new Regex("\"(input_tokens|output_tokens|cache_creation_input_tokens)\":(\\d+)");var stamp=new Regex("\"timestamp\":\"([^\"]+)\"");
   foreach(var file in Directory.EnumerateFiles(root,"*.jsonl",SearchOption.AllDirectories)){
    if(File.GetLastWriteTime(file)<since)continue;
    try{using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var r=new StreamReader(stream)){string line;while((line=r.ReadLine())!=null){if(line.IndexOf("\"usage\"",StringComparison.Ordinal)<0)continue;var t=stamp.Match(line);DateTime when;if(!t.Success||!DateTime.TryParse(t.Groups[1].Value,null,System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal,out when)||when.ToLocalTime()<since)continue;messages++;foreach(Match m in tokenField.Matches(line))tokens+=long.Parse(m.Groups[2].Value);}}}catch(Exception){}
   }
   return Tuple.Create(tokens,messages);
  }
  public static readonly string[] Keys={"claude-tokens-today","claude-tokens-week","claude-messages-today","web","file","json-file"};
 }
}
