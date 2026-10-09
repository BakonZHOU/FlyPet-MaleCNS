using System.Diagnostics;

namespace FlyPet;

public sealed class FileSearchWindow : Form
{
    readonly Label summary;
    readonly ListBox results;
    CancellationTokenSource? searchCancellation;

    public FileSearchWindow()
    {
        Theme.Form(this);Text="FlyPet · 文件搜索";ClientSize=new(760,460);MinimumSize=new(600,340);StartPosition=FormStartPosition.CenterScreen;
        Controls.Add(Theme.Label("本地文件搜索",20,18,700,34,18,Theme.Accent));
        summary=Theme.Label("",22,58,700,30,10,Theme.Muted);Controls.Add(summary);
        results=new ListBox{Location=new(22,94),Size=new(716,310),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,Font=Theme.Font(10),HorizontalScrollbar=true};
        results.DoubleClick+=(_,_)=>OpenSelected();Controls.Add(results);
        var open=Theme.Button("打开选中的文件",22,414,144,OpenSelected,true);open.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;Controls.Add(open);
        FormClosing+=(_,_)=>searchCancellation?.Cancel();
    }

    public async void Search(string query,IEnumerable<string> roots)
    {
        searchCancellation?.Cancel();searchCancellation=new CancellationTokenSource(TimeSpan.FromSeconds(15));var cancellation=searchCancellation;
        results.Items.Clear();summary.Text=$"正在本机搜索“{query}”…";Show();BringToFront();Activate();
        try
        {
            var found=await LocalFileSearch.SearchAsync(roots,query,cancellation.Token);
            if(IsDisposed||cancellation.IsCancellationRequested)return;
            foreach(var path in found)results.Items.Add(path);
            summary.Text=found.Count==0?$"没有在已配置的本地文件夹中找到“{query}”。":$"找到 {found.Count} 个结果；双击即可打开。";
        }
        catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed)summary.Text="搜索失败："+e.Message;}
    }

    void OpenSelected()
    {
        if(results.SelectedItem is not string path)return;
        try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
        catch(Exception e){MessageBox.Show(this,"无法打开文件："+e.Message,"FlyPet");}
    }

    protected override void Dispose(bool disposing){if(disposing)searchCancellation?.Cancel();base.Dispose(disposing);}
}
