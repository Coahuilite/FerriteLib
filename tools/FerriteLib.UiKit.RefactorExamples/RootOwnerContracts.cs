using System;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.AuthorExamples;
static partial class Program
{
    static void RootOwnerContracts()
    {
        int reads=0;
        var current=new UiBindings();current.BindReadOnly("visible",()=>{reads++;return true;});current.BindReadOnly("width",()=>{reads++;return 100f;});
        var wrong=new UiBindings();wrong.BindReadOnly("visible",()=>true);wrong.BindReadOnly("width",()=>100f);
        foreach(string body in new[]{"<Column Id='root' VisibleBind='visible'/>","<Surface Id='root' WidthBind='width'/>","<Column Id='outer'><Surface Id='root' VisibleBind='visible' WidthBind='width'/></Column>"})
        {
            bool refused=false;
            try{using var host=new UiHost("author",UiLayoutManifest.Parse("<UiPage Schema='2' Source='author'>"+body+"</UiPage>"),new WrongOwner(current,wrong),new UiTheme(),new Metrics(),new Translation());}
            catch(UiContractException error){refused=error.Scope=="author"&&error.ElementId=="root"&&error.ElementPath.Contains("root")&&error.Message.Contains("BindingOwner");}
            Check(refused&&reads==0,"PROXY shared root/container owner validation is located and precedes getters: "+body);
        }
        current.BindReadOnly<System.Collections.Generic.IReadOnlyList<string>>("rows",()=>new[]{"a"});wrong.BindReadOnly<System.Collections.Generic.IReadOnlyList<string>>("rows",()=>new[]{"a"});
        string visible=new UiBindingAddress("rows","a","visible").Key,width=new UiBindingAddress("rows","a","width").Key;
        current.BindReadOnly(visible,()=>{reads++;return true;});current.BindReadOnly(width,()=>{reads++;return 100f;});
        wrong.BindReadOnly(visible,()=>true);wrong.BindReadOnly(width,()=>100f);
        var template=UiLayoutManifest.Parse("<UiPage Schema='2' Source='author'><Templates><Column Id='row' VisibleBind='visible'><Surface Id='cell' WidthBind='width' AlignX='Left' Height='20'/></Column></Templates><Repeat Id='repeat' Items='rows' Template='row'/></UiPage>");
        using(var host=new UiHost("author",template,new ForwardingBindings(current),new UiTheme(),new Metrics(),new Translation()))
        {
            var snapshot=host.MeasureAndArrange(new UiLayoutPoint(300,100));
            Check(snapshot.RectById.ContainsKey("row#a")&&snapshot.RectById["cell#a"].Width==100,"PROXY template container consumes correctly scoped Value endpoints without inventing a collection owner");
        }
        int before=reads;bool templateRefused=false;
        try
        {
            using var host=new UiHost("author",template,new WrongOwner(current,wrong),new UiTheme(),new Metrics(),new Translation());
            host.MeasureAndArrange(new UiLayoutPoint(300,100));
        }
        catch(UiContractException error){templateRefused=error.ElementId=="row#a"&&error.ElementPath.Contains("row#a")&&error.Message.Contains("BindingOwner");}
        Check(templateRefused&&reads==before,"PROXY materialized template container refuses mismatched scoped endpoints before reading visibility/width");
    }
}

