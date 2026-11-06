using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace WinCanvas {
 public partial class Studio {
  string aiWarning;AiConfig aiConfig;AiClient aiClient;SecretStore aiKeys,aiHistory;ChatSession chat;CancellationTokenSource pulling;
  // Small, popular Ollama models: name, label.
  static readonly string[][] Models={new[]{"gemma3:1b","Fastest · about 0.8 GB"},new[]{"llama3.2:3b","Balanced · about 2 GB"},new[]{"qwen2.5:7b","Smartest · about 4.7 GB"}};
  void SetupAi() {
   aiConfig=new AiConfig();aiKeys=new SecretStore(Path.Combine(folder,"credentials"));aiHistory=new SecretStore(Path.Combine(folder,"chat-history"));aiClient=new AiClient();
   string path=Path.Combine(folder,"ai-settings.json");
   try{if(File.Exists(path)){aiConfig=JsonFile.Read<AiConfig>(path);if(aiConfig==null)throw new InvalidDataException("Empty AI config");aiConfig.Validate(false);}}
   catch(Exception){aiConfig=new AiConfig();aiWarning="AI settings could not load, so AI is back on Auto. Existing files are retained.";}
   // Key-based cloud settings from older versions move to the keyless Auto mode.
   if(!AiConfig.Keyless.Contains(aiConfig.Provider)&&aiConfig.Provider!="Compatible"){aiConfig.Provider="Auto";aiConfig.Endpoint="http://localhost:11434/api/chat";aiConfig.Model="";}
   chat=new ChatSession(aiConfig,new LocalAi(aiClient),aiKeys,aiHistory);
   try{chat.Load();}catch(Exception){aiWarning="Saved chat could not be read. You can clear it on the AI page.";}
  }
  void OpenAiSetup(){Show();Activate();Page("AI");}
  string AiSummary(){return aiConfig.Provider=="Auto"?"Auto":aiConfig.Provider=="Claude app"||aiConfig.Provider=="ChatGPT app"?aiConfig.Provider:aiConfig.Provider=="Claude Code"?"Claude":aiConfig.Provider=="Codex"?"ChatGPT":aiConfig.Provider=="Browser"?aiConfig.BrowserSite+" (web)":aiConfig.Provider;}
  // Every change on this page saves at once; there are no secrets to protect.
  void SaveAi(Action<AiConfig> change) {
   if(chat.Busy)throw new InvalidOperationException("Stop the current reply first.");
   var draft=aiConfig.Copy();change(draft);draft.Validate(false);string previous=aiConfig.ChatId;
   if(!draft.RememberChat)aiHistory.Delete(draft.ChatId);
   JsonFile.Save(Path.Combine(folder,"ai-settings.json"),draft);aiConfig=draft;chat.Config=aiConfig;if(previous!=aiConfig.ChatId)chat.Load();
  }
  Button Choice(string title,string description,bool selected,Action action) {
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});grid.ColumnDefinitions.Add(new ColumnDefinition());
   var dot=new Border{Width=16,Height=16,CornerRadius=new CornerRadius(8),BorderBrush=Brush(selected?Ui.Ink:Ui.Faint),BorderThickness=new Thickness(1.5),VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,2,0,0),Child=selected?new System.Windows.Shapes.Ellipse{Width=8,Height=8,Fill=Brush(Ui.Ink)}:null};grid.Children.Add(dot);
   var text=new StackPanel();text.Children.Add(new TextBlock{Text=title,FontWeight=FontWeights.SemiBold});text.Children.Add(new TextBlock{Text=description,FontSize=12,Foreground=Brush(Ui.Soft),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,0)});Grid.SetColumn(text,1);grid.Children.Add(text);
   var b=new Button{Content=grid,HorizontalContentAlignment=HorizontalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(16,14,16,14),Margin=new Thickness(0,0,0,8)};
   if(selected){b.BorderBrush=Brush(Ui.Accent);b.BorderThickness=new Thickness(1.5);}b.Click+=(s,e)=>Safe(action);return b;
  }
  TextBlock Badge(Panel parent){var badge=Text("Checking…",12,Ui.Faint);badge.Margin=new Thickness(0,0,0,8);parent.Children.Add(badge);return badge;}
  void AiSettings() {
   Header("AI","No API keys. The island answers with free local AI, your Claude subscription, or your browser.");
   var how=Card("How should the island answer?","");
   foreach(var route in AiRoutes())how.Children.Add(AiRouteChoice(route,()=>{Page(current);Report("AI set to "+AiSummary()+".");}));
   var open=new WrapPanel();open.Children.Add(Primary("Open the AI island",()=>{Profile.Island=true;Save();Overlays.Profile=Profile;if(!Overlays.Active)Overlays.Start();Overlays.OpenAssistant();Report("Island open. Type a question and press Ctrl+Enter.");}));how.Children.Add(open);
   // Free local AI
   var ollama=Card("Free local AI  ·  Ollama","Download Ollama once (free), then pick a model below. WinDotra finds it automatically.");
   var ollamaState=Badge(ollama);var installed=new ComboBox{Visibility=Visibility.Collapsed};ollama.Children.Add(installed);
   var ollamaActions=new WrapPanel();ollamaActions.Children.Add(Button("Get Ollama ↗",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://ollama.com/download"){UseShellExecute=true})));ollama.Children.Add(ollamaActions);
   var download=Text("Download a model",13,Ui.Ink);download.FontWeight=FontWeights.SemiBold;download.Margin=new Thickness(0,14,0,4);ollama.Children.Add(download);
   var models=new WrapPanel();ollama.Children.Add(models);var bar=new ProgressBar{Maximum=1,Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,4)};ollama.Children.Add(bar);var pullState=Text("",12,Ui.Soft);ollama.Children.Add(pullState);
   foreach(var item in Models){string name=item[0];var chip=Button(name+"   "+item[1],()=>{});chip.Padding=new Thickness(12,7,12,7);chip.Margin=new Thickness(0,4,8,4);
    chip.Click+=async(s,e)=>{
     if(pulling!=null){pullState.Text="A download is already running.";return;}
     pulling=new CancellationTokenSource();bar.Visibility=Visibility.Visible;bar.Value=0;pullState.Text="Downloading "+name+"… keep WinDotra open.";
     try{await Ollama.Pull(aiConfig.Endpoint,name,new Progress<double>(v=>{bar.Value=v;pullState.Text="Downloading "+name+"…  "+(v*100).ToString("0")+"%";}),pulling.Token);pullState.Text=name+" is ready. The island will use it.";SaveAi(c=>c.Model=name);Report(name+" downloaded.");}
     catch(Exception ex){pullState.Text=ex is System.Net.Http.HttpRequestException?"Ollama isn't running. Install it with Get Ollama, open it, then try again.":ex.Message;}
     finally{pulling.Dispose();pulling=null;bar.Visibility=Visibility.Collapsed;}
    };models.Children.Add(chip);}
   var address=Text("Ollama address  ·  "+aiConfig.Endpoint,12,Ui.Faint);ollama.Children.Add(address);
   DetectOllama(ollamaState,installed);
   // Claude subscription
   var claude=Card("Your Claude subscription","Install the Claude command-line tool once, then run claude in a terminal one time to sign in with your Claude account. No key needed.");
   string exe=ClaudeCode.Find();var claudeState=Text(exe!=null?"Found and ready  ·  "+exe:"Not installed yet.",12,exe!=null?"#16A34A":Ui.Faint);claudeState.Margin=new Thickness(0,0,0,8);claude.Children.Add(claudeState);
   var install=new WrapPanel();const string command="irm https://claude.ai/install.ps1 | iex";
   install.Children.Add(Button("Copy install command",()=>{Clipboard.SetText(command);Report("Copied. Paste it into Windows PowerShell and press Enter, then run: claude");}));install.Children.Add(Button("Check again",()=>Page(current)));claude.Children.Add(install);
   claude.Children.Add(Text("Install command for PowerShell:  "+command,12,Ui.Faint));
   var gpt=Card("Your ChatGPT account","Install the Codex command-line tool once and sign in with your ChatGPT account. Then ChatGPT answers right in the island. No key needed.");
   string codex=Codex.Find();var gptState=Text(codex!=null?"Found and ready  ·  "+codex:"Not installed yet.",12,codex!=null?"#16A34A":Ui.Faint);gptState.Margin=new Thickness(0,0,0,8);gpt.Children.Add(gptState);
   var gptActions=new WrapPanel();const string npm="npm install -g @openai/codex";
   gptActions.Children.Add(Button("Copy install command",()=>{Clipboard.SetText(npm);Report("Copied. Paste it into a terminal (needs Node.js), then click Sign in.");}));gptActions.Children.Add(Button("Sign in to ChatGPT",Codex.SignIn));gptActions.Children.Add(Button("Check again",()=>Page(current)));gpt.Children.Add(gptActions);
   // Browser
   var browser=Card("Browser","Used by Browser mode, and by Auto when nothing local is installed. Uses whatever account you're signed in with.");
   var sites=new WrapPanel();foreach(string site in new[]{"Claude","ChatGPT"}){string pick=site;sites.Children.Add(Chip(site=="Claude"?"claude.ai":"chatgpt.com",aiConfig.BrowserSite==site,()=>{SaveAi(c=>c.BrowserSite=pick);Page(current);}));}browser.Children.Add(sites);
   // Assistant
   var assistant=Card("Assistant","Instructions for local AI and Claude. Saved when you click away.");
   var instructions=new TextBox{Text=aiConfig.Instructions,MaxLength=4000,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=78,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,4,0,6)};
   instructions.LostKeyboardFocus+=(s,e)=>{if(instructions.Text!=aiConfig.Instructions)Safe(()=>{SaveAi(c=>c.Instructions=instructions.Text);Report("Instructions saved.");});};assistant.Children.Add(instructions);
   Check(assistant,"Share my screen automatically when I ask about it (“what's on my screen?”, “look at this”)",aiConfig.AutoScreen,v=>Safe(()=>{SaveAi(c=>c.AutoScreen=v);RefreshDesktop();}));
   Check(assistant,"Always share my screen with every message",aiConfig.AlwaysScreen,v=>Safe(()=>{SaveAi(c=>c.AlwaysScreen=v);RefreshDesktop();}));
   Check(assistant,"Let the AI suggest actions on this PC (open apps, music, volume, lock, timers). You click Run on each one",aiConfig.AllowActions,v=>Safe(()=>SaveAi(c=>c.AllowActions=v)));
   Check(assistant,"Remember chats on this PC (encrypted for your Windows account)",aiConfig.RememberChat,v=>Safe(()=>SaveAi(c=>c.RememberChat=v)));
   Slider(assistant,"Longest local reply (tokens)",aiConfig.MaxTokens,128,8192,v=>{});var lengthSlider=assistant.Children.OfType<Slider>().Last();lengthSlider.PreviewMouseUp+=(s,e)=>Safe(()=>SaveAi(c=>c.MaxTokens=(int)lengthSlider.Value));
   var privacy=Card("Privacy","Only what you type, the recent chat, and these instructions are sent, and only when you press Send. The island never reads your screen, clipboard, files, notes, or tasks. Replies are text only.");
   privacy.Children.Add(Button("Delete saved chats",()=>{chat.Clear();aiHistory.DeleteAll();Report("Saved chats deleted from this PC.");}));
   if(aiKeys.Exists(new AiConfig{Provider="Claude",Endpoint="https://api.anthropic.com/v1/messages"}.CredentialId)||aiKeys.Exists(new AiConfig{Provider="OpenAI",Endpoint="https://api.openai.com/v1/chat/completions"}.CredentialId))
    privacy.Children.Add(Button("Delete API keys from older versions",()=>{foreach(string file in Directory.Exists(Path.Combine(folder,"credentials"))?Directory.GetFiles(Path.Combine(folder,"credentials"),"*.protected"):new string[0])File.Delete(file);Report("Old API keys deleted.");Page(current);}));
  }
  async void DetectOllama(TextBlock state,ComboBox installed) {
   var found=await Ollama.Models(aiConfig.Endpoint);
   // Without a UI synchronization context (smoke tests) the await resumes on a pool thread, so hop back explicitly.
   state.Dispatcher.BeginInvoke(new Action(()=>ShowOllama(state,installed,found)));
  }
  void ShowOllama(TextBlock state,ComboBox installed,System.Collections.Generic.List<string> found) {
   if(found.Count==0){state.Text="Not found. Install and open Ollama, then download a model.";return;}
   state.Text="Running  ·  "+found.Count+" model"+(found.Count==1?"":"s")+" installed";state.Foreground=Brush("#16A34A");
   installed.ItemsSource=found;installed.SelectedItem=found.Contains(aiConfig.Model)?aiConfig.Model:found[0];installed.Visibility=Visibility.Visible;
   installed.SelectionChanged+=(s,e)=>{if(installed.SelectedItem!=null)Safe(()=>{SaveAi(c=>c.Model=installed.SelectedItem.ToString());Report("Local model set to "+aiConfig.Model+".");});};
  }
 }
}
