using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading;

namespace BouncyIcons {
    sealed class ChatWindow:Form {
        readonly PetWindow pet;
        ChatSettings settings;
        readonly TextBox transcript=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
        readonly TextBox input=new TextBox{Multiline=true,MaxLength=4000};
        readonly TextBox endpoint=new TextBox(),model=new TextBox(),key=new TextBox{UseSystemPasswordChar=true},persona=new TextBox{Multiline=true,MaxLength=2000};
        readonly Button send=new Button{Text="发送"},cancel=new Button{Text="取消请求",Enabled=false},clear=new Button{Text="清空聊天"};
        readonly Label status=new Label();
        readonly TabControl tabs=new TabControl();
        readonly TabPage config=new TabPage("API 设置（全局）");
        readonly List<ChatMessage> history=new List<ChatMessage>();
        CancellationTokenSource pending;
        public ChatWindow(PetWindow p){
            pet=p;Text="和挂件聊天 · "+p.Entry.Name;Icon=Program.AppIcon;ClientSize=new Size(660,610);Font=new Font("Microsoft YaHei",10);StartPosition=FormStartPosition.CenterParent;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;tabs.Dock=DockStyle.Fill;Controls.Add(tabs);var chat=new TabPage("聊天");tabs.TabPages.Add(chat);tabs.TabPages.Add(config);
            transcript.SetBounds(15,15,620,355);chat.Controls.Add(transcript);input.SetBounds(15,383,620,85);chat.Controls.Add(input);
            send.SetBounds(525,480,110,34);cancel.SetBounds(400,480,115,34);clear.SetBounds(15,480,110,34);chat.Controls.AddRange(new Control[]{send,cancel,clear});
            status.SetBounds(15,525,620,50);status.Text="仅在点击发送时联网；发送聊天文字和人设，不发送 GIF 或应用路径。";chat.Controls.Add(status);
            Row("完整聊天接口地址（例如 https://你的服务/v1/chat/completions）",endpoint,15);
            Row("模型名称（按服务商填写）",model,90);Row("API 密钥（本机服务可留空）",key,165);
            Row("挂件人设",persona,240);persona.Height=100;
            var save=new Button{Text="保存设置",Location=new Point(15,390),Size=new Size(140,36)};config.Controls.Add(save);
            config.Controls.Add(new Label{Text="支持 Chat Completions 兼容接口。密钥用 Windows 当前用户加密保存。\n设置不会发起请求；服务商可能按调用计费。\n聊天记录只保留在此窗口，关闭后清空。",Location=new Point(15,450),Size=new Size(610,90)});
            try{settings=ChatSettings.Load();endpoint.Text=settings.Endpoint;model.Text=settings.Model;key.Text=settings.GetKey();persona.Text=settings.Persona;}catch{settings=new ChatSettings();persona.Text=settings.Persona;status.Text="旧 API 配置无法读取，请在 API 设置中重新保存。";}
            if(String.IsNullOrWhiteSpace(settings.Endpoint))tabs.SelectedTab=config;
            save.Click+=(s,e)=>{try{var next=new ChatSettings{Endpoint=endpoint.Text.Trim(),Model=model.Text.Trim(),Persona=persona.Text};next.SetKey(key.Text);next.Save();settings=next;history.Clear();transcript.Clear();status.Text="设置已保存；聊天内容会发送到："+next.Validate().Host;tabs.SelectedTab=chat;}catch(Exception ex){MessageBox.Show(ex is ArgumentException?ex.Message:"保存失败，请检查本机配置目录。","API 设置");}};
            send.Click+=Send;cancel.Click+=(s,e)=>{if(pending!=null)pending.Cancel();};clear.Click+=(s,e)=>{history.Clear();transcript.Clear();};
            FormClosing+=(s,e)=>{if(pending!=null)pending.Cancel();};
        }
        void Row(string title,TextBox box,int y){config.Controls.Add(new Label{Text=title,Location=new Point(15,y),Size=new Size(610,26)});box.SetBounds(15,y+30,610,28);config.Controls.Add(box);}
        async void Send(object sender,EventArgs args){
            string text=input.Text.Trim();if(text.Length==0||pending!=null)return;
            try{settings.Validate();}catch(ArgumentException ex){status.Text=ex.Message;tabs.SelectedTab=config;return;}
            var request=new List<ChatMessage>(history);request.Add(new ChatMessage("user",text));
            pending=new CancellationTokenSource();var tokenSource=pending;send.Enabled=clear.Enabled=config.Enabled=input.Enabled=false;cancel.Enabled=true;status.Text="正在回复…";
            try{
                string reply=await ChatService.SendAsync(settings,request,tokenSource.Token);
                if(IsDisposed||tokenSource.IsCancellationRequested)return;
                history.Add(new ChatMessage("user",text));history.Add(new ChatMessage("assistant",reply));while(history.Count>12)history.RemoveRange(0,2);
                transcript.AppendText("你："+text+Environment.NewLine+"挂件："+reply+Environment.NewLine+Environment.NewLine);input.Clear();
                if(!pet.IsDisposed)pet.ShowChatReply(reply);status.Text="已回复。最近 6 轮用于连续对话；关闭窗口清空。";
            }catch(OperationCanceledException){if(!IsDisposed)status.Text=tokenSource.IsCancellationRequested?"已取消，输入内容保留。":"请求超时，输入内容保留。";}
            catch(Exception ex){if(!IsDisposed)status.Text=ex is InvalidOperationException?ex.Message:"连接失败。请检查接口地址、网络和密钥；输入内容保留。";}
            finally{pending=null;tokenSource.Dispose();if(!IsDisposed){send.Enabled=clear.Enabled=config.Enabled=input.Enabled=true;cancel.Enabled=false;}}
        }
    }
}
