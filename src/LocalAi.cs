using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WinCanvas {
 // Raised when no local engine is available; the island then offers the browser instead.
 public class NoLocalAiException : InvalidOperationException { public NoLocalAiException(string message):base(message){} }
 // Claude Code is installed but not signed in (or its sign-in expired).
 public class ClaudeSignInException : InvalidOperationException { public ClaudeSignInException():base("Claude needs you to sign in. Click Sign in, then type /login in the window that opens."){} }

 // Keyless AI: Ollama on this PC, the Claude Code CLI signed in with the user's Claude account, or the browser.
 public class LocalAi : IAiTransport {
  readonly IAiTransport http;
  public LocalAi(IAiTransport http){this.http=http;}
  public async Task<string> Send(AiConfig config,string key,IList<ChatMessage> messages,IProgress<string> progress,CancellationToken token) {
   string engine=config.Provider;
   if(engine=="Claude app"||engine=="ChatGPT app")throw new NoLocalAiException("Opening "+engine+".");
   if(engine=="Browser")throw new NoLocalAiException("Browser mode: your question opens in "+config.BrowserSite+".");
   if(engine=="Auto"||engine=="Ollama") {
    var models=await Ollama.Models(config.Endpoint).ConfigureAwait(false);
    if(models.Count>0){var local=config.Copy();local.Provider="Ollama";if(String.IsNullOrEmpty(local.Model)||!models.Contains(local.Model))local.Model=models[0];return await http.Send(local,"",messages,progress,token).ConfigureAwait(false);}
    if(engine=="Ollama")throw new NoLocalAiException("Ollama isn't running or has no model yet. Open Studio → AI to download one.");
   }
   if(engine=="Auto"||engine=="Claude Code") {
    string exe=ClaudeCode.Find();
    if(exe!=null) {
     if(engine=="Claude Code")return await ClaudeCode.Send(exe,config,messages,progress,token).ConfigureAwait(false);
     // In Auto, a Claude that isn't signed in (or fails) falls through to the browser instead of leaving an error.
     try{return await ClaudeCode.Send(exe,config,messages,progress,token).ConfigureAwait(false);}
     catch(OperationCanceledException){throw;}
     catch(ClaudeSignInException){throw new NoLocalAiException("Claude isn't signed in yet, so your question opens in "+config.BrowserSite+".");}
     catch(Exception e){throw new NoLocalAiException("Claude couldn't answer ("+e.Message+"), so your question opens in "+config.BrowserSite+".");}
    }
    if(engine=="Claude Code")throw new NoLocalAiException("The Claude command-line tool isn't installed. Studio → AI shows how.");
   }
   if(engine=="Auto"||engine=="Codex") {
    // ChatGPT through the Codex command-line tool, signed in with the user's ChatGPT account.
    string codex=Codex.Find();
    if(codex!=null) {
     if(engine=="Codex")return await Codex.Send(codex,config,messages,progress,token).ConfigureAwait(false);
     try{return await Codex.Send(codex,config,messages,progress,token).ConfigureAwait(false);}
     catch(OperationCanceledException){throw;}
     catch(Exception e){throw new NoLocalAiException("ChatGPT couldn't answer ("+e.Message+"), so your question opens in "+config.BrowserSite+".");}
    }
    if(engine=="Codex")throw new NoLocalAiException("The Codex command-line tool (ChatGPT) isn't installed. WinDotra → AI shows how.");
   }
   if(engine=="Auto")throw new NoLocalAiException("No local AI found yet, so your question opens in "+config.BrowserSite+".");
   return await http.Send(config,key,messages,progress,token).ConfigureAwait(false);
  }
 }

 public static class Ollama {
  static readonly HttpClient client=new HttpClient{Timeout=TimeSpan.FromSeconds(3)};
  public static string Base(string endpoint){var uri=new Uri(endpoint);return uri.GetLeftPart(UriPartial.Authority);}
  public static async Task<List<string>> Models(string endpoint) {
   try {
    string json=await client.GetStringAsync(Base(endpoint)+"/api/tags").ConfigureAwait(false);
    var root=AiClient.Obj(new JavaScriptSerializer().DeserializeObject(json));var list=AiClient.Get(root,"models") as object[];
    return list==null?new List<string>():list.Select(m=>AiClient.Str(AiClient.Obj(m),"name")).Where(n=>!String.IsNullOrEmpty(n)).ToList();
   } catch(Exception) { return new List<string>(); }
  }
  // Streams /api/pull progress as 0..1; Ollama downloads and verifies the model itself.
  public static async Task Pull(string endpoint,string model,IProgress<double> progress,CancellationToken token) {
   using(var http=new HttpClient{Timeout=Timeout.InfiniteTimeSpan})
   using(var request=new HttpRequestMessage(HttpMethod.Post,Base(endpoint)+"/api/pull"){Content=new StringContent(new JavaScriptSerializer().Serialize(new Dictionary<string,object>{{"model",model},{"stream",true}}),Encoding.UTF8,"application/json")})
   using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false)) {
    if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Ollama could not start the download (HTTP "+(int)response.StatusCode+").");
    using(var reader=new StreamReader(await response.Content.ReadAsStreamAsync().ConfigureAwait(false))) {
     string line;
     while((line=await reader.ReadLineAsync().ConfigureAwait(false))!=null) {
      token.ThrowIfCancellationRequested();if(String.IsNullOrWhiteSpace(line))continue;
      var item=AiClient.Obj(new JavaScriptSerializer().DeserializeObject(line));
      if(AiClient.Get(item,"error")!=null)throw new InvalidOperationException("Ollama: "+AiClient.Str(item,"error"));
      object total=AiClient.Get(item,"total"),done=AiClient.Get(item,"completed");
      if(total!=null&&done!=null){double t=Convert.ToDouble(total),d=Convert.ToDouble(done);if(t>0)progress.Report(d/t);}
      if(AiClient.Str(item,"status")=="success"){progress.Report(1);return;}
     }
    }
   }
  }
 }

 public static class ClaudeCode {
  public static string Find() {
   var candidates=new List<string>{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".local","bin","claude.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"npm","claude.cmd")};
   foreach(string dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(';')) {if(dir.Trim().Length==0)continue;try{candidates.Add(Path.Combine(dir.Trim(),"claude.exe"));candidates.Add(Path.Combine(dir.Trim(),"claude.cmd"));}catch(ArgumentException){}}
   return candidates.FirstOrDefault(File.Exists);
  }
  // Opens Claude in its own console window so the user can sign in with /login.
  public static void SignIn(){string exe=Find();if(exe==null)throw new InvalidOperationException("The Claude command-line tool isn't installed. WinDotra → AI shows how.");Process.Start(new ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});}
  // Runs `claude -p` in an empty folder so it has nothing on disk to look at; the prompt goes in through stdin.
  public static async Task<string> Send(string exe,AiConfig config,IList<ChatMessage> messages,IProgress<string> progress,CancellationToken token) {
   string sandbox=Path.Combine(Path.GetTempPath(),"WinDotra-claude");Directory.CreateDirectory(sandbox);
   var prompt=new StringBuilder(config.Instructions).AppendLine().AppendLine();
   if(messages.Count>1){prompt.AppendLine("Conversation so far:");foreach(var m in messages.Take(messages.Count-1))prompt.AppendLine((m.role=="user"?"User: ":"Assistant: ")+m.content);prompt.AppendLine();}
   if(config.ImagePath!=null&&File.Exists(config.ImagePath)){File.Copy(config.ImagePath,Path.Combine(sandbox,"screen.png"),true);prompt.AppendLine("The user shared a screenshot of their screen. It is saved as screen.png in the current folder; look at it with your Read tool before answering.").AppendLine();}
   prompt.AppendLine("Reply to this message:").Append(messages[messages.Count-1].content);
   bool script=exe.EndsWith(".cmd",StringComparison.OrdinalIgnoreCase);
   var info=new ProcessStartInfo(script?"cmd.exe":exe,(script?"/d /c \""+exe+"\" ":"")+"-p --output-format text"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=sandbox,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   using(var process=Process.Start(info))
   using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token))
   using(deadline.Token.Register(()=>{try{if(!process.HasExited)process.Kill();}catch(Exception){}})) {
    deadline.CancelAfter(TimeSpan.FromMinutes(3));
    byte[] input=new UTF8Encoding(false).GetBytes(prompt.ToString());await process.StandardInput.BaseStream.WriteAsync(input,0,input.Length).ConfigureAwait(false);process.StandardInput.Close();
    var errors=process.StandardError.ReadToEndAsync();var answer=new StringBuilder();var buffer=new char[512];int read;
    while((read=await process.StandardOutput.ReadAsync(buffer,0,buffer.Length).ConfigureAwait(false))>0){if(answer.Length+read>16000)throw new InvalidDataException("Reply exceeded 16,000 characters.");string chunk=new string(buffer,0,read);answer.Append(chunk);progress.Report(chunk);}
    process.WaitForExit();
    if(token.IsCancellationRequested)throw new OperationCanceledException(token);
    if(deadline.IsCancellationRequested)throw new TimeoutException("Claude took over three minutes. Try a shorter request.");
    string detail=(await errors.ConfigureAwait(false)).Trim(),text=answer.ToString().Trim();
    // Claude Code prints sign-in problems as ordinary output (sometimes with exit code 0), so check the text too.
    if(System.Text.RegularExpressions.Regex.IsMatch((text.Length<400?text:"")+" "+detail,"(?i)failed to authenticate|oauth|/login|not logged in|log in to|invalid api key|authentication_error"))throw new ClaudeSignInException();
    if(process.ExitCode!=0)throw new InvalidOperationException("Claude couldn't answer"+(detail.Length>0?": "+detail.Split('\n')[0]:"."));
    if(text.Length==0)throw new InvalidDataException("Claude returned no text.");
    return answer.ToString().Trim();
   }
  }
 }

 // ChatGPT in the island: `codex exec` with the user's ChatGPT sign-in, read-only, in an empty folder, nothing saved.
 public static class Codex {
  public static string Find() {
   var candidates=new List<string>{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"npm","codex.cmd")};
   foreach(string dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(';')) {if(dir.Trim().Length==0)continue;try{candidates.Add(Path.Combine(dir.Trim(),"codex.exe"));candidates.Add(Path.Combine(dir.Trim(),"codex.cmd"));}catch(ArgumentException){}}
   return candidates.FirstOrDefault(File.Exists);
  }
  public static void SignIn(){string exe=Find();if(exe==null)throw new InvalidOperationException("The Codex command-line tool isn't installed. WinDotra → AI shows how.");Process.Start(new ProcessStartInfo("cmd.exe","/k \""+exe+"\" login"){UseShellExecute=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});}
  public static async Task<string> Send(string exe,AiConfig config,IList<ChatMessage> messages,IProgress<string> progress,CancellationToken token) {
   string sandbox=Path.Combine(Path.GetTempPath(),"WinDotra-codex");Directory.CreateDirectory(sandbox);string output=Path.Combine(sandbox,"reply-"+Guid.NewGuid().ToString("N")+".txt");
   var prompt=new StringBuilder(config.Instructions).AppendLine().AppendLine("Answer directly in plain text. Do not run commands or edit files.").AppendLine();
   if(messages.Count>1){prompt.AppendLine("Conversation so far:");foreach(var m in messages.Take(messages.Count-1))prompt.AppendLine((m.role=="user"?"User: ":"Assistant: ")+m.content);prompt.AppendLine();}
   string image="";if(config.ImagePath!=null&&File.Exists(config.ImagePath)){string copy=Path.Combine(sandbox,"screen.png");File.Copy(config.ImagePath,copy,true);image=" -i \""+copy+"\"";prompt.AppendLine("The attached image is a screenshot of the user's screen.").AppendLine();}
   prompt.AppendLine("Reply to this message:").Append(messages[messages.Count-1].content);
   bool script=exe.EndsWith(".cmd",StringComparison.OrdinalIgnoreCase);
   string args="exec --skip-git-repo-check --ephemeral --ignore-user-config -s read-only --color never"+image+" -C \""+sandbox+"\" -o \""+output+"\" -";
   var info=new ProcessStartInfo(script?"cmd.exe":exe,script?"/d /s /c \"\""+exe+"\" "+args+"\"":args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=sandbox,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   try {
    using(var process=Process.Start(info))
    using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token))
    using(deadline.Token.Register(()=>{try{if(!process.HasExited)process.Kill();}catch(Exception){}})) {
     deadline.CancelAfter(TimeSpan.FromMinutes(3));
     byte[] input=new UTF8Encoding(false).GetBytes(prompt.ToString());await process.StandardInput.BaseStream.WriteAsync(input,0,input.Length).ConfigureAwait(false);process.StandardInput.Close();
     var errors=process.StandardError.ReadToEndAsync();var log=await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);process.WaitForExit();
     if(token.IsCancellationRequested)throw new OperationCanceledException(token);
     if(deadline.IsCancellationRequested)throw new TimeoutException("ChatGPT took over three minutes. Try a shorter request.");
     string detail=(await errors.ConfigureAwait(false)+" "+log).Trim();
     string text=File.Exists(output)?File.ReadAllText(output,Encoding.UTF8).Trim():"";
     if(text.Length==0){if(System.Text.RegularExpressions.Regex.IsMatch(detail,"(?i)not logged in|login required|please log in|401"))throw new InvalidOperationException("ChatGPT needs you to sign in: WinDotra → AI → Sign in to ChatGPT.");var line=detail.Split('\n').LastOrDefault(l=>l.Contains("ERROR"))??"";throw new InvalidOperationException("ChatGPT couldn't answer"+(line.Length>0?": "+line.Trim():"."));}
     if(text.Length>16000)text=text.Substring(0,16000);
     progress.Report(text);return text;
    }
   } finally { try{if(File.Exists(output))File.Delete(output);}catch(Exception){} }
  }
 }

 public static class BrowserAi {
  // Opens the site with the question pre-filled. Very long questions go to the clipboard instead of the address bar.
  public static string Open(string site,string question) {
   string root=site=="ChatGPT"?"https://chatgpt.com/":"https://claude.ai/new";bool fits=question.Length<=1500;
   if(!fits)System.Windows.Clipboard.SetText(question);
   Process.Start(new ProcessStartInfo(fits?root+"?q="+Uri.EscapeDataString(question):root){UseShellExecute=true});
   return fits?"Opened "+site+" with your question filled in. Answers appear in your browser.":"Your question is on the clipboard. Paste it into "+site+".";
  }
 }
}
