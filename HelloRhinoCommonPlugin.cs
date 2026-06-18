using System;
using HelloRhinoCommon.Runtime;
using HelloRhinoCommon.UI;
using Rhino;
using Rhino.PlugIns;
using Rhino.UI;

namespace HelloRhinoCommon
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class HelloRhinoCommonPlugin : PlugIn
    {
        private bool _openedPropertiesOnStartup;

        public HelloRhinoCommonPlugin()
        {
            Instance = this;
        }
        
        ///<summary>Gets the only instance of the HelloRhinoCommonPlugin plug-in.</summary>
        public static HelloRhinoCommonPlugin Instance { get; private set; } = null!;

        // Load on demand (first command / UI request), NOT at startup. Loading AtStartup
        // and force-loading Grasshopper that early (before Rhino's UI is ready) destabilizes
        // Grasshopper -- it can leave GH unable to open. By the time the user invokes this
        // plug-in, Rhino and Grasshopper are fully initialized and loading is safe.
        public override PlugInLoadTime LoadTime => PlugInLoadTime.WhenNeeded;

        internal ModifierEngine Engine { get; private set; } = null!;

        // Grasshopper's Rhino plug-in GUID. We depend on its assemblies (GH_Document, etc.).
        private static readonly Guid GrasshopperPlugInId =
            new Guid("b45a29b1-4343-4035-989e-044e8580d9cf");

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            try
            {
                // The engine's type graph references Grasshopper types (GH_Document, etc.).
                // Ensure Grasshopper is loaded before we touch the engine so those types
                // resolve. This runs on demand (LoadTime = WhenNeeded), i.e. after Rhino is
                // fully up, so loading Grasshopper here is safe.
                if (!PlugIn.LoadPlugIn(GrasshopperPlugInId))
                {
                    errorMessage = "HelloRhinoCommon requires Grasshopper, which could not be loaded.";
                    return LoadReturnCode.ErrorShowDialog;
                }

                Engine = new ModifierEngine();
                RhinoApp.Initialized += OnRhinoInitialized;
                RhinoApp.Idle += OnRhinoIdle;
                return LoadReturnCode.Success;
            }
            catch (Exception ex)
            {
                // Surface the real exception instead of the generic popup, so the next
                // failure tells us exactly what went wrong.
                errorMessage = "HelloRhinoCommon failed to initialize: " + ex;
                return LoadReturnCode.ErrorShowDialog;
            }
        }

        protected override void OnShutdown()
        {
            RhinoApp.Initialized -= OnRhinoInitialized;
            RhinoApp.Idle -= OnRhinoIdle;
            Engine.Dispose();
            base.OnShutdown();
        }

        protected override void ObjectPropertiesPages(ObjectPropertiesPageCollection collection)
        {
            collection.Add(new ModifierObjectPropertiesPage());
        }

        private void OnRhinoInitialized(object? sender, EventArgs e)
        {
            RhinoApp.Initialized -= OnRhinoInitialized;
            RhinoApp.InvokeOnUiThread(OpenPropertiesPanelOnStartup);
        }

        private void OnRhinoIdle(object? sender, EventArgs e)
        {
            if (_openedPropertiesOnStartup)
            {
                RhinoApp.Idle -= OnRhinoIdle;
                return;
            }

            RhinoApp.Idle -= OnRhinoIdle;
            OpenPropertiesPanelOnStartup();
        }

        private void OpenPropertiesPanelOnStartup()
        {
            if (_openedPropertiesOnStartup)
            {
                return;
            }

            _openedPropertiesOnStartup = true;

            if (!Panels.IsPanelVisible(PanelIds.ObjectProperties))
            {
                Panels.OpenPanel(PanelIds.ObjectProperties);
            }
        }
    }
}
