using System;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.UI;

namespace HelloRhinoCommon.UI;

public sealed class ModifierObjectPropertiesPage : ObjectPropertiesPage
{
    private readonly Panel _host = new();
    private ModifierStackPanel? _panel;

    public override string EnglishPageTitle => "GGH Stack";

    // Without this override SupportedTypes defaults to "nothing", so no selected
    // object ever qualifies and the page tab never appears, even though ShouldDisplay
    // returns true. These mirror ModifierEngine.IsSupportedGeometryObject
    // (Point, Curve, Brep, Extrusion, Mesh, SubD).
    public override ObjectType SupportedTypes =>
        ObjectType.Point | ObjectType.Curve | ObjectType.Brep |
        ObjectType.Extrusion | ObjectType.Mesh | ObjectType.SubD;

    public override string PageIconEmbeddedResourceString => "HelloRhinoCommon.Logo_3.ico";

    public override int Index => int.MaxValue;

    public override object PageControl
    {
        get
        {
            EnsurePanel();
            return _host;
        }
    }

    public override bool ShouldDisplay(ObjectPropertiesPageEventArgs e)
    {
        return true;
    }

    public override void UpdatePage(ObjectPropertiesPageEventArgs e)
    {
        EnsurePanel();
        if (_panel is null)
        {
            return;
        }

        try
        {
            _panel.RefreshNow();
        }
        catch (Exception ex)
        {
            ShowError("Failed to update modifier panel.", ex);
        }
    }

    private void EnsurePanel()
    {
        if (_panel is not null)
        {
            return;
        }

        try
        {
            _panel = new ModifierStackPanel();
            _host.Content = _panel;
        }
        catch (Exception ex)
        {
            ShowError("Failed to initialize modifier panel.", ex);
        }
    }

    private void ShowError(string prefix, Exception ex)
    {
        _panel = null;
        RhinoApp.WriteLine($"GGH: {prefix} {ex}");
        _host.Content = new Label
        {
            Text = $"{prefix} {ex.Message}",
            Wrap = WrapMode.Word,
        };
    }
}
