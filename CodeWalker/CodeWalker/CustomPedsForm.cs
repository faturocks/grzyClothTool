using CodeWalker.GameFiles;
using CodeWalker.Properties;
using CodeWalker.Rendering;
using CodeWalker.Utils;
using CodeWalker.World;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Color = SharpDX.Color;
using System.Runtime.InteropServices;

namespace CodeWalker
{
    public partial class CustomPedsForm : Form, DXForm
    {
        // Win32 API declarations for fallback screenshot
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private const uint SRCCOPY = 0x00CC0020;

        public Form Form { get { return this; } } //for DXForm/DXManager use

        public Renderer Renderer = null;
        public object RenderSyncRoot { get { return Renderer.RenderSyncRoot; } }

        public volatile bool formopen = false;

        public bool isLoading = true;
        volatile bool running = false;
        volatile bool pauserendering = false;
        //volatile bool initialised = false;

        Stopwatch frametimer = new Stopwatch();
        Camera camera;
        Timecycle timecycle;
        Weather weather;
        Clouds clouds;

        Entity camEntity = new Entity();


        bool MouseLButtonDown = false;
        bool MouseRButtonDown = false;
        int MouseX;
        int MouseY;
        System.Drawing.Point MouseDownPoint;
        System.Drawing.Point MouseLastPoint;

        public GameFileCache GameFileCache { get; } = new GameFileCache(Settings.Default.CacheSize, Settings.Default.CacheTime, GTAFolder.CurrentGTAFolder, Settings.Default.DLC, false, "levels;anim;audio;data;");


        InputManager Input = new InputManager();


        bool initedOk = false;



        bool toolsPanelResizing = false;
        int toolsPanelResizeStartX = 0;
        int toolsPanelResizeStartLeft = 0;
        int toolsPanelResizeStartRight = 0;


        public string PedModel = "mp_f_freemode_01";
        Ped SelectedPed = new Ped();
        public Dictionary<string, Drawable> SavedDrawables = new Dictionary<string, Drawable>();
        public Dictionary<string, Drawable> LoadedDrawables = new Dictionary<string, Drawable>();
        public Dictionary<Drawable, TextureDictionary> LoadedTextures = new Dictionary<Drawable, TextureDictionary>();
        public Dictionary<Drawable, TextureDictionary> SavedTextures = new Dictionary<Drawable, TextureDictionary>();

        string liveTexturePath = null;
        DateTime liveTextureLastWriteTime;
        Texture LiveTexture = new Texture();

        List<List<ComponentComboItem>> ComponentComboBoxes;
        private Dictionary<string, List<VertexTypePC>> floorVerticesDict = new Dictionary<string, List<VertexTypePC>>();
        private readonly Vector3[] floorVertices = new Vector3[]
        {
            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, 1.0f,  -1.0f),

            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),

            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, 1.0f,  -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),

            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f, -1.0f)
        };

        private bool highheelvaluechanged = false;
        private bool renderOnlySelected = false;

        private System.Windows.Forms.Timer autoRotateTimer;
        private float autoRotateAngle = 0f;
        private const float AutoRotateSpeed = 1.0f;

        private bool originalToolsPanelVisibility = false;
        private bool originalOnlySelectedState = false;
        private Color originalClearColour;
        private bool isTransparentScreenshotMode = false;
        
        // Fields for resolution management
        private bool isFixedResolutionMode = false;
        private int originalWidth = 0;
        private int originalHeight = 0;
        private const int FIXED_SCREENSHOT_RESOLUTION = 1024;

        // Add flag to track transparent rendering mode
        private bool isTransparentRenderingMode = false;

        public class ComponentComboItem
        {
            public MCPVDrawblData DrawableData { get; set; }
            public int AlternativeIndex { get; set; }
            public int TextureIndex { get; set; }
            public ComponentComboItem(MCPVDrawblData drawableData, int altIndex = 0, int textureIndex = -1)
            {
                DrawableData = drawableData;
                AlternativeIndex = altIndex;
                TextureIndex = textureIndex;
            }
            public override string ToString()
            {
                if (DrawableData == null) return TextureIndex.ToString();
                var itemname = DrawableData.GetDrawableName(AlternativeIndex);
                if (DrawableData.TexData?.Length > 0) return itemname + " + " + DrawableData.GetTextureSuffix(TextureIndex);
                return itemname;
            }
            public string DrawableName
            {
                get
                {
                    return DrawableData?.GetDrawableName(AlternativeIndex) ?? "error";
                }
            }
            public string TextureName
            {
                get
                {
                    return DrawableData?.GetTextureName(TextureIndex);
                }
            }
        }

        public CustomPedsForm()
        {

            InitializeComponent();

            ComponentComboBoxes = new List<List<ComponentComboItem>>
            {
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
                new List<ComponentComboItem>(),
            };

            Renderer = new Renderer(this, GameFileCache);
            camera = Renderer.camera;
            timecycle = Renderer.timecycle;
            weather = Renderer.weather;
            clouds = Renderer.clouds;

            initedOk = Renderer.Init();

            Renderer.controllightdir = !Settings.Default.Skydome;
            Renderer.rendercollisionmeshes = false;
            Renderer.renderclouds = false;
            //Renderer.renderclouds = true;
            //Renderer.individualcloudfrag = "Contrails";
            Renderer.rendermoon = true;
            Renderer.renderskeletons = false;
            Renderer.SelectionFlagsTestAll = true;
            Renderer.swaphemisphere = true;


            Renderer.renderskydome = false;
            Renderer.renderhdtextures = true;

            autoRotateTimer = new System.Windows.Forms.Timer();
            autoRotateTimer.Interval = 16; // About 60 FPS
            autoRotateTimer.Tick += AutoRotateTimer_Tick;
        }

        public override void Refresh()
        {
            base.Refresh();
            UpdateModelsUI();
        }

        public void InitScene(Device device)
        {
            int width = ClientSize.Width;
            int height = ClientSize.Height;

            try
            {
                Renderer.DeviceCreated(device, width, height);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading shaders!\n" + ex.ToString());
                return;
            }


            SetDefaultCameraPosition();

            Renderer.shaders.deferred = false; //no point using this here yet
            Renderer.shaders.AnisotropicFiltering = true;
            Renderer.shaders.hdr = false;

            LoadSettings();


            formopen = true;
            new Thread(new ThreadStart(ContentThread)).Start();

            frametimer.Start();

            floorVerticesDict = new Dictionary<string, List<VertexTypePC>>
            {
                { "DrawableFloor", CreateFloorVertices(new Color(50, 50, 50, 255)) },
                { "PreviewFloor", CreateFloorVertices(new Color(90, 90, 90, 255)) }
            };
        }

        private List<VertexTypePC> CreateFloorVertices(Color color)
        {
            var verticesList = new List<VertexTypePC>(floorVertices.Length);
            uint colorValue = (uint)color.ToRgba();

            foreach (var vertex in floorVertices)
            {
                verticesList.Add(new VertexTypePC
                {
                    Position = vertex,
                    Colour = colorValue
                });
            }

            return verticesList;
        }

        public void CleanupScene()
        {
            formopen = false;

            Renderer.DeviceDestroyed();

            int count = 0;
            while (running && (count < 5000)) //wait for the content thread to exit gracefully
            {
                Thread.Sleep(1);
                count++;
            }
        }

        public void RenderScene(DeviceContext context)
        {
            float elapsed = (float)frametimer.Elapsed.TotalSeconds;
            frametimer.Restart();

            if (pauserendering) return;

            GameFileCache.BeginFrame();

            if (!Monitor.TryEnter(Renderer.RenderSyncRoot, 50))
            { return; } //couldn't get a lock, try again next time

            UpdateControlInputs(elapsed);

            Renderer.Update(elapsed, MouseLastPoint.X, MouseLastPoint.Y);

            Renderer.BeginRender(context);

            // Ensure transparent flags are set if DefScene/HDR were just created by BeginRender
            if (isTransparentRenderingMode)
            {
                if (Renderer.shaders?.DefScene != null && !Renderer.shaders.DefScene.UseTransparentBackground)
                {
                    Renderer.shaders.DefScene.UseTransparentBackground = true;
                }
                
                if (Renderer.shaders?.HDR != null && !Renderer.shaders.HDR.UseTransparentBackground)
                {
                    Renderer.shaders.HDR.UseTransparentBackground = true;
                }
            }

            Renderer.RenderSkyAndClouds();

            RenderSelectedItems();

            RenderFloor();

            Renderer.SelectedDrawable = null;

            Renderer.RenderPed(SelectedPed);

            Renderer.RenderQueued();

            Renderer.RenderFinalPass();

            Renderer.EndRender();

            Monitor.Exit(Renderer.RenderSyncRoot);
        }

        private void RenderSelectedItems()
        {
            foreach(var drawable in LoadedDrawables.Values)
            {
                if (LoadedTextures.TryGetValue(drawable, out var texture))
                {
                    RenderSelectedItem(drawable, texture);
                }
            }

            if (SavedDrawables.Count > 0)
            {
                foreach (var drawable in SavedDrawables.Values)
                {
                    if (LoadedDrawables.TryGetValue(drawable.Name, out var loadedDrawable) && loadedDrawable.Name.Equals(drawable.Name))
                    {
                        continue;
                    }
                    if (!SavedTextures.ContainsKey(drawable))
                    {
                        continue;
                    }

                    RenderSelectedItem(drawable, SavedTextures[drawable]);
                }
            }
        }
        private void RenderSelectedItem(Drawable d, TextureDictionary t)
        {
            // dirty hack to render drawable only when all other drawables are rendered, it fixes issue that props sometimes are not rendered attached to the head
            if (Renderer.RenderedDrawablesDict.Count < 4) return;

            var isProp = d.Name.StartsWith("p_");
            d.Owner = SelectedPed;

            if(d.Skeleton == null || d.Skeleton.Bones.Items.Length == 0)
            {
                d.Skeleton = SelectedPed.Skeleton.Clone();
            }

            if(liveTexturePath != null)
            {
                var files = Directory.GetFiles(liveTexturePath, "*.dds");
                if(files.Length > 0)
                {
                    var file = files[0];
                    var fileinfo = new FileInfo(file);
                    var lastwritetime = fileinfo.LastWriteTime;
                    
                    if (lastwritetime > liveTextureLastWriteTime)
                    {
                        liveTextureLastWriteTime = lastwritetime;

                        LiveTexture = DDSIO.GetTexture(File.ReadAllBytes(file));

                    }
                    Renderer.RenderDrawable(d, null, SelectedPed.RenderEntity, 0, null, LiveTexture, SelectedPed.AnimClip, null, null, isProp, true);
                }
                return;
            }

            if(t.Textures.data_items.Count() == 0)
            {
                return;
            }

            Renderer.RenderDrawable(d, null, SelectedPed.RenderEntity, 0, t, t.Textures.data_items[0], SelectedPed.AnimClip, null, null, isProp, true);
        }

        private void RenderFloor()
        {
            if (Renderer.renderfloor)
            {
                if (floorVerticesDict.TryGetValue("DrawableFloor", out var floorVerticesList))
                {
                    if (Renderer.SelDrawable != null && Renderer.SelDrawable.IsHighHeelsEnabled)
                    {
                        List<VertexTypePC> newFloorVerticesList = new List<VertexTypePC>();

                        if (highheelvaluechanged || newFloorVerticesList.Count == 0)
                        {
                            newFloorVerticesList.Clear();

                            foreach (var ver in floorVerticesList)
                            {
                                var newPosition = new Vector3(ver.Position.X, ver.Position.Y, ver.Position.Z - Renderer.SelDrawable.HighHeelsValue);
                                newFloorVerticesList.Add(new VertexTypePC
                                {
                                    Position = newPosition,
                                    Colour = ver.Colour
                                });
                            }

                            highheelvaluechanged = false;
                        }

                        Renderer.RenderTriangles(newFloorVerticesList);
                    }
                    else
                    {
                        Renderer.RenderTriangles(floorVerticesList);
                    }
                }
            }

            if (floorCheckbox.Checked)
            {
                if (floorVerticesDict.TryGetValue("PreviewFloor", out var previewFloorVerticesList))
                {
                    List<VertexTypePC> newPreviewFloorVerticesList = new List<VertexTypePC>();
                    float floorUpDownValue = (float)floorUpDown.Value / 10;

                    foreach (var ver in previewFloorVerticesList)
                    {
                        var newPosition = new Vector3(ver.Position.X, ver.Position.Y, ver.Position.Z - floorUpDownValue);
                        newPreviewFloorVerticesList.Add(new VertexTypePC
                        {
                            Position = newPosition,
                            Colour = ver.Colour
                        });
                    }

                    Renderer.RenderTriangles(newPreviewFloorVerticesList);
                }
            }
        }

        public void BuffersResized(int w, int h)
        {
            Renderer.BuffersResized(w, h);
        }
        public bool ConfirmQuit()
        {
            return true;
        }

        private void Init()
        {
            //called from PedForm_Load

            if (!initedOk)
            {
                Close();
                return;
            }

            MouseWheel += PedsForm_MouseWheel;

            if (!GTAFolder.UpdateGTAFolder(true))
            {
                Close();
                return;
            }

            ShaderParamNames[] texsamplers = RenderableGeometry.GetTextureSamplerList();
            foreach (var texsampler in texsamplers)
            {
                TextureSamplerComboBox.Items.Add(texsampler);
            }

            Input.Init();

            Renderer.Start();
        }

        private void ContentThread()
        {
            //main content loading thread.
            running = true;

            UpdateStatus("Scanning...");

            try
            {
                GTA5Keys.LoadFromPath(GTAFolder.CurrentGTAFolder, Settings.Default.Key);
            }
            catch
            {
                MessageBox.Show("Keys not found! This shouldn't happen.");
                Close();
                return;
            }

            GameFileCache.EnableDlc = false;
            GameFileCache.EnableMods = false;
            GameFileCache.LoadPeds = true;
            GameFileCache.LoadVehicles = false;
            GameFileCache.LoadArchetypes = false;//to speed things up a little
            GameFileCache.BuildExtendedJenkIndex = false;//to speed things up a little
            GameFileCache.DoFullStringIndex = true;//to get all global text from DLC...
            GameFileCache.Init(UpdateStatus, LogError);

            UpdateGlobalPedsUI();


            LoadWorld();
            isLoading = false;
            Task.Run(() => {
                while (formopen && !IsDisposed) //renderer content loop
                {
                    bool rcItemsPending = Renderer.ContentThreadProc();
                    if (!rcItemsPending)
                    {
                        Thread.Sleep(1); //sleep if there's nothing to do
                    }
                }
            });

            while (formopen && !IsDisposed) //main asset loop
            {
                bool fcItemsPending = GameFileCache.ContentThreadProc();
                if (!fcItemsPending)
                {
                    Thread.Sleep(1); //sleep if there's nothing to do
                }
            }

            GameFileCache.Clear();
            running = false;
        }

        private void LoadSettings()
        {
            var s = Settings.Default;
            WireframeCheckBox.Checked = false;
            HDRRenderingCheckBox.Checked = false;
            ShadowsCheckBox.Checked = true;

            EnableAnimationCheckBox.Checked = false;
            ClipComboBox.Enabled = false;
            ClipDictComboBox.Enabled = false;
            EnableRootMotionCheckBox.Enabled = false;
            PlaybackSpeedTrackBar.Enabled = false;

            RenderModeComboBox.SelectedIndex = Math.Max(RenderModeComboBox.FindString(s.RenderMode), 0);
            TextureSamplerComboBox.SelectedIndex = Math.Max(TextureSamplerComboBox.FindString(s.RenderTextureSampler), 0);
            TextureCoordsComboBox.SelectedIndex = Math.Max(TextureCoordsComboBox.FindString(s.RenderTextureSamplerCoord), 0);
        }
        private void LoadWorld()
        {
            UpdateStatus("Loading timecycles...");
            timecycle.Init(GameFileCache, UpdateStatus);
            timecycle.SetTime(Renderer.timeofday);

            UpdateStatus("Loading materials...");
            BoundsMaterialTypes.Init(GameFileCache);

            UpdateStatus("Loading weather...");
            weather.Init(GameFileCache, UpdateStatus, timecycle);

            UpdateStatus("Loading clouds...");
            clouds.Init(GameFileCache, UpdateStatus, weather);

        }

        private void UpdateStatus(string text)
        {
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => { UpdateStatus(text); }));
                }
                else
                {
                    StatusLabel.Text = text;
                }
            }
            catch { }
        }
        private void LogError(string text)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => { LogError(text); }));
                }
                else
                {
                    //TODO: error logging..
                    ConsoleTextBox.AppendText(text + "\r\n");
                    //StatusLabel.Text = text;
                    //MessageBox.Show(text);
                }
            }
            catch { }
        }

        private void UpdateMousePosition(MouseEventArgs e)
        {
            MouseX = e.X;
            MouseY = e.Y;
            MouseLastPoint = e.Location;
        }

        private void RotateCam(int dx, int dy)
        {
            camera.MouseRotate(dx, dy);
        }

        private void MoveCameraToView(Vector3 pos, float rad)
        {
            //move the camera to a default place where the given sphere is fully visible.

            // Improved distance calculation for better framing
            // Ensure minimum radius for very small items
            rad = Math.Max(rad, 0.5f);
            
            // Calculate distance based on bounding sphere with proper scaling
            // This ensures the object fills most of the screen but with appropriate padding
            float distance = rad * 2.5f; // Increased multiplier for better distance
            
            // Ensure minimum distance to prevent being too close
            distance = Math.Max(distance, 1.5f);
            
            // Ensure maximum distance for very large items
            distance = Math.Min(distance, 15.0f);

            camera.FollowEntity.Position = pos;
            camera.TargetDistance = distance;
            camera.CurrentDistance = distance;

            camera.UpdateProj = true;
        }
        private void AddDrawableTreeNode(DrawableBase drawable, string name, bool check)
        {
            var tnode = TexturesTreeView.Nodes.Add(name);
            var dnode = ModelsTreeView.Nodes.Add(name);
            dnode.Tag = drawable;
            dnode.Checked = check;

            if (name.Contains("Selected"))
            {
                string drawableTypeName;
                string[] nameParts = name.Split(' ')[1].Trim('(', ')').Split('_');

                if (nameParts.Length <= 1 || nameParts[0] != "p")
                {
                    drawableTypeName = nameParts[0];
                    var sameName = ModelsTreeView.Nodes.Cast<TreeNode>().Where(n =>
                        n.Text.Contains(drawableTypeName) &&
                        !n.Text.Contains("Selected") &&
                        !n.Text.Contains("Saved")
                    ).ToList();
                    if (sameName.Count > 0)
                    {
                        foreach (var node in sameName)
                        {
                            node.Checked = !node.Checked;
                        }
                    }
                }
            }

            AddDrawableModelsTreeNodes(drawable.DrawableModels?.High, "High Detail", true, dnode, tnode);
            AddDrawableModelsTreeNodes(drawable.DrawableModels?.Med, "Medium Detail", false, dnode, tnode);
            AddDrawableModelsTreeNodes(drawable.DrawableModels?.Low, "Low Detail", false, dnode, tnode);
            AddDrawableModelsTreeNodes(drawable.DrawableModels?.VLow, "Very Low Detail", false, dnode, tnode);

        }
        private void AddDrawableModelsTreeNodes(DrawableModel[] models, string prefix, bool check, TreeNode parentDrawableNode = null, TreeNode parentTextureNode = null)
        {
            if (models == null) return;

            for (int mi = 0; mi < models.Length; mi++)
            {
                var tnc = (parentDrawableNode != null) ? parentDrawableNode.Nodes : ModelsTreeView.Nodes;

                var model = models[mi];
                string mprefix = prefix + " " + (mi + 1).ToString();
                var mnode = tnc.Add(mprefix + " " + model.ToString());
                mnode.Tag = model;
                mnode.Checked = check;

                var ttnc = (parentTextureNode != null) ? parentTextureNode.Nodes : TexturesTreeView.Nodes;
                var tmnode = ttnc.Add(mprefix + " " + model.ToString());
                tmnode.Tag = model;

                if (!check)
                {
                    Renderer.SelectionModelDrawFlags[model] = false;
                }

                if (model.Geometries == null) continue;

                foreach (var geom in model.Geometries)
                {
                    var gname = geom.ToString();
                    var gnode = mnode.Nodes.Add(gname);
                    gnode.Tag = geom;
                    gnode.Checked = true;// check;

                    var tgnode = tmnode.Nodes.Add(gname);
                    tgnode.Tag = geom;

                    if ((geom.Shader != null) && (geom.Shader.ParametersList != null) && (geom.Shader.ParametersList.Hashes != null))
                    {
                        var pl = geom.Shader.ParametersList;
                        var h = pl.Hashes;
                        var p = pl.Parameters;
                        for (int ip = 0; ip < h.Length; ip++)
                        {
                            var hash = pl.Hashes[ip];
                            var parm = pl.Parameters[ip];
                            var tex = parm.Data as TextureBase;
                            if (tex != null)
                            {
                                var t = tex as Texture;
                                var tstr = tex.Name.Trim();
                                if (t != null)
                                {
                                    tstr = string.Format("{0} ({1}x{2}, embedded)", tex.Name, t.Width, t.Height);
                                }
                                var tnode = tgnode.Nodes.Add(hash.ToString().Trim() + ": " + tstr);
                                tnode.Tag = tex;
                            }
                        }
                        tgnode.Expand();
                    }

                }

                mnode.Expand();
                tmnode.Expand();
            }
        }
        private void UpdateSelectionDrawFlags(TreeNode node)
        {
            //update the selection draw flags depending on tag and checked/unchecked
            var drwbl = node.Tag as DrawableBase;
            var model = node.Tag as DrawableModel;
            var geom = node.Tag as DrawableGeometry;
            bool rem = node.Checked;
            lock (Renderer.RenderSyncRoot)
            {
                if (drwbl != null)
                {
                    if (rem)
                    {
                        if (Renderer.SelectionDrawableDrawFlags.ContainsKey(drwbl))
                        {
                            Renderer.SelectionDrawableDrawFlags.Remove(drwbl);
                        }
                    }
                    else
                    {
                        Renderer.SelectionDrawableDrawFlags[drwbl] = false;
                    }
                }
                if (model != null)
                {
                    if (rem)
                    {
                        if (Renderer.SelectionModelDrawFlags.ContainsKey(model))
                        {
                            Renderer.SelectionModelDrawFlags.Remove(model);
                        }
                    }
                    else
                    {
                        Renderer.SelectionModelDrawFlags[model] = false;
                    }
                }
                if (geom != null)
                {
                    if (rem)
                    {
                        if (Renderer.SelectionGeometryDrawFlags.ContainsKey(geom))
                        {
                            Renderer.SelectionGeometryDrawFlags.Remove(geom);
                        }
                    }
                    else
                    {
                        Renderer.SelectionGeometryDrawFlags[geom] = false;
                    }
                }
                //updateArchetypeStatus = true;
            }
        }

        private void UpdateGlobalPedsUI()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => { UpdateGlobalPedsUI(); }));
            }
            else
            {

                var index = PedModel == "mp_f_freemode_01" ? 0 : 1;

                PedNameComboBox.Items.Add("mp_f_freemode_01");
                PedNameComboBox.Items.Add("mp_m_freemode_01");
                PedNameComboBox.SelectedIndex = index;
            }

        }

        private void UpdateModelsUI()
        {
            Renderer.SelectionDrawableDrawFlags.Clear();
            Renderer.SelectionModelDrawFlags.Clear();
            Renderer.SelectionGeometryDrawFlags.Clear();
            ModelsTreeView.Nodes.Clear();
            ModelsTreeView.ShowRootLines = true;
            TexturesTreeView.Nodes.Clear();
            TexturesTreeView.ShowRootLines = true;

            if (SelectedPed == null) return;

            // The following could be combined into a single for loop with a conditional check inside
            if (!renderOnlySelected)
            {
                for (int i = 0; i < 12; i++)
                {
                    var drawable = SelectedPed.Drawables[i];
                    var drawablename = SelectedPed.DrawableNames[i];

                    if (drawable != null)
                    {
                        AddDrawableTreeNode(drawable, drawablename, true);
                    }
                }
            }
            else
            {
                // Disabling rendering for all drawables.
                // Code below this for loop will override this flag for the drawable(s) we want to render.
                for (int i = 0; i < 12; i++)
                {
                    var drawable = SelectedPed.Drawables[i];

                    if (drawable != null)
                    {
                        Renderer.SelectionDrawableDrawFlags[drawable] = false;
                    }
                }
            }

            foreach (var drawable in LoadedDrawables.Values)
            {
                AddDrawableTreeNode(drawable, $"Selected ({drawable.Name})", true);
            }

            if (SavedDrawables.Count > 0)
            {
                foreach (var drawable in SavedDrawables.Values)
                {
                    if (LoadedDrawables.Values.Contains(drawable)) continue;
                    AddDrawableTreeNode(drawable, $"Saved ({drawable.Name})", true);
                }
            }
        }

        public void LoadAnimsForModel(string pedname)
        {
            if(pedname == "mp_m_freemode_01")
            {
                ClipDictComboBox.Text = "move_m@generic";
            } 
            else if(pedname == "mp_f_freemode_01")
            {
                ClipDictComboBox.Text = "move_f@generic";
            }

            ClipComboBox.Text = "idle";
            LoadClipDict(ClipDictComboBox.Text);
            SelectClip("idle");
        }

        public void LoadPed(string pedname = "")
        {
            if (string.IsNullOrEmpty(pedname))
            {
                pedname = PedNameComboBox.Text;
            }
            PedNameComboBox.Text = pedname;

            var pedhash = JenkHash.GenHash(pedname.ToLowerInvariant());
            var pedchange = SelectedPed.NameHash != pedhash;

            SelectedPed.Init(pedname, GameFileCache);

            LoadModel(SelectedPed.Yft, pedchange);
            if(EnableAnimationCheckBox.Checked)
            {
                LoadAnimsForModel(pedname);
            }
            
            var vi = SelectedPed.Ymt?.VariationInfo;
            if (vi != null)
            {
                for (int i = 0; i < 12; i++)
                {
                    PopulateCompCombo(ComponentComboBoxes.ElementAt(i), vi.GetComponentData(i));
                }
            }

            head_updown.Maximum = ComponentComboBoxes[0].Count - 1;
            berd_updown.Maximum = ComponentComboBoxes[1].Count - 1;
            hair_updown.Maximum = ComponentComboBoxes[2].Count - 1;
            uppr_updown.Maximum = ComponentComboBoxes[3].Count - 1;
            lowr_updown.Maximum = ComponentComboBoxes[4].Count - 1;
            feet_updown.Maximum = ComponentComboBoxes[6].Count - 1;

            var index = PedNameComboBox.SelectedIndex;
            head_updown.Value = GetCompSettingsValue("HeadComp", index);
            berd_updown.Value = GetCompSettingsValue("BerdComp", index);
            hair_updown.Value = GetCompSettingsValue("HairComp", index);
            uppr_updown.Value = GetCompSettingsValue("UpprComp", index);
            lowr_updown.Value = GetCompSettingsValue("LowrComp", index);
            feet_updown.Value = GetCompSettingsValue("FeetComp", index);

            SetComponentDrawable(0, (int)head_updown.Value);
            SetComponentDrawable(1, (int)berd_updown.Value);
            SetComponentDrawable(2, (int)hair_updown.Value);
            SetComponentDrawable(3, (int)uppr_updown.Value);
            SetComponentDrawable(4, (int)lowr_updown.Value);
            SetComponentDrawable(6, (int)feet_updown.Value);


            UpdateModelsUI();
        }
       
        public int GetCompSettingsValue(string name, int index)
        {

            string[] settings = (Settings.Default[name] as string).Split(';');
            if (settings != null)
            {
                return Convert.ToInt32(settings[index]);
            }
            return 0;
        }

        public void UpdateSelectedDrawable(Drawable d, TextureDictionary t, Dictionary<string, string> updates)
        {
            Renderer.SelDrawable = d;
            Renderer.SelectedDrawableChanged = true;

            foreach (var update in updates)
            {
                var value = update.Value.ToString().ToLower();
                switch (update.Key)
                {
                    case "EnableKeepPreview":
                        var v = bool.Parse(value);
                        //if loadeddrawables already contains drawable, remove it
                        if (v == true && !SavedDrawables.ContainsKey(d.Name))
                        {
                            SavedDrawables.Add(d.Name, d);
                            SavedTextures.Add(d, t);
                            UpdateModelsUI();
                        }
                        else if (v == false && SavedDrawables.ContainsKey(d.Name))
                        {
                            SavedDrawables.Remove(d.Name);
                            SavedTextures.Remove(d);
                            UpdateModelsUI();
                        }
                        break;
                    case "EnableHairScale":
                        Renderer.SelDrawable.IsHairScaleEnabled = bool.Parse(value);
                        break;
                    case "HairScaleValue":
                        Renderer.SelDrawable.HairScaleValue = Convert.ToSingle(value);
                        break;
                    case "EnableHighHeels":
                        Renderer.SelDrawable.IsHighHeelsEnabled = bool.Parse(value);
                        break;
                    case "HighHeelsValue":
                        Renderer.SelDrawable.HighHeelsValue = Convert.ToSingle(value) / 10;
                        highheelvaluechanged = true;
                        break;
                    case "GenderChanged":
                        LoadPed(PedModel);
                        break;
                    default:
                        break;
                }
            }
        }

        public void LoadModel(YftFile yft, bool movecamera = true)
        {
            if (yft == null) return;

            //FileName = yft.Name;
            //Yft = yft;

            var dr = yft.Fragment?.Drawable;
            if (movecamera && (dr != null))
            {
                MoveCameraToView(dr.BoundingCenter, dr.BoundingSphereRadius);
            }

            //UpdateModelsUI(yft.Fragment.Drawable);
        }

        private void PopulateCompCombo(List<ComponentComboItem> c, MCPVComponentData compData)
        {
            if (compData?.DrawblData3 == null) return;
            foreach (var item in compData.DrawblData3)
            {
                c.Add(new ComponentComboItem(item));
            }
        }

        private void SetComponentDrawable(int compIndex, int itemIndex)
        {
            var s = ComponentComboBoxes[compIndex][itemIndex];

            SelectedPed.SetComponentDrawable(compIndex, s.DrawableName, s.TextureName, GameFileCache);

            UpdateModelsUI();
        }

        private void LoadClipDict(string name)
        {
            var ycdhash = JenkHash.GenHash(name.ToLowerInvariant());
            var ycd = GameFileCache.GetYcd(ycdhash);
            while ((ycd != null) && (!ycd.Loaded))
            {
                Thread.Sleep(1);//kinda hacky
                ycd = GameFileCache.GetYcd(ycdhash);
            }

            SelectedPed.Ycd = ycd;

            ClipComboBox.Items.Clear();
            ClipComboBox.Items.Add("");

            if (ycd?.ClipMapEntries == null)
            {
                ClipComboBox.SelectedIndex = 0;
                SelectedPed.AnimClip = null;
                return;
            }

            List<string> items = new List<string>();
            foreach (var cme in ycd.ClipMapEntries)
            {
                if (cme.Clip != null)
                {
                    items.Add(cme.Clip.ShortName);
                }
            }

            items.Sort();
            foreach (var item in items)
            {
                ClipComboBox.Items.Add(item);
            }
        }

        private void SelectClip(string name)
        {
            MetaHash cliphash = JenkHash.GenHash(name);
            ClipMapEntry cme = null;
            SelectedPed.Ycd?.ClipMap?.TryGetValue(cliphash, out cme);
            SelectedPed.AnimClip = cme;

            PlaybackSpeedTrackBar.Value = 60;
            UpdatePlaybackSpeedLabel();
        }
        private void UpdateTimeOfDayLabel()
        {
            int v = TimeOfDayTrackBar.Value;
            float fh = v / 60.0f;
            int ih = (int)fh;
            int im = v - (ih * 60);
            if (ih == 24) ih = 0;
            TimeOfDayLabel.Text = string.Format("{0:00}:{1:00}", ih, im);
        }

        private void UpdateControlInputs(float elapsed)
        {
            if (elapsed > 0.1f) elapsed = 0.1f;

            var s = Settings.Default;

            float moveSpeed = 2.0f;


            Input.Update();

            if (Input.ShiftPressed)
            {
                moveSpeed *= 5.0f;
            }
            if (Input.CtrlPressed)
            {
                moveSpeed *= 0.2f;
            }

            Vector3 movevec = Input.KeyboardMoveVec(false);

            if (Input.xbenable)
            {
                movevec.X += Input.xblx;
                movevec.Z -= Input.xbly;
                moveSpeed *= (1.0f + (Math.Min(Math.Max(Input.xblt, 0.0f), 1.0f) * 15.0f)); //boost with left trigger
                if (Input.ControllerButtonPressed(GamepadButtonFlags.A | GamepadButtonFlags.RightShoulder | GamepadButtonFlags.LeftShoulder))
                {
                    moveSpeed *= 5.0f;
                }
            }
            {
                //normal movement
                movevec *= elapsed * moveSpeed * Math.Min(camera.TargetDistance, 50.0f);
            }


            Vector3 movewvec = camera.ViewInvQuaternion.Multiply(movevec);
            camEntity.Position += movewvec;

            if (Input.xbenable)
            {
                camera.ControllerRotate(Input.xbrx, Input.xbry, elapsed);

                float zoom = 0.0f;
                float zoomspd = s.XInputZoomSpeed;
                float zoomamt = zoomspd * elapsed;
                if (Input.ControllerButtonPressed(GamepadButtonFlags.DPadUp)) zoom += zoomamt;
                if (Input.ControllerButtonPressed(GamepadButtonFlags.DPadDown)) zoom -= zoomamt;

                camera.ControllerZoom(zoom);
            }
        }

        private void PedsForm_Load(object sender, EventArgs e)
        {
            Init();
        }

        private void PedsForm_MouseDown(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left: MouseLButtonDown = true; break;
                case MouseButtons.Right: MouseRButtonDown = true; break;
            }

            if (!ToolsPanelShowButton.Focused)
            {
                ToolsPanelShowButton.Focus(); //make sure no textboxes etc are focused!
            }

            MouseDownPoint = e.Location;
            MouseLastPoint = MouseDownPoint;

            if (MouseLButtonDown)
            {
            }

            if (MouseRButtonDown)
            {
                //SelectMousedItem();
            }

            MouseX = e.X; //to stop jumps happening on mousedown, sometimes the last MouseMove event was somewhere else... (eg after clicked a menu)
            MouseY = e.Y;
        }

        private void PedsForm_MouseUp(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left: MouseLButtonDown = false; break;
                case MouseButtons.Right: MouseRButtonDown = false; break;
            }

        }

        private void PedsForm_MouseMove(object sender, MouseEventArgs e)
        {
            int dx = e.X - MouseX;
            int dy = e.Y - MouseY;

            {
                if (MouseLButtonDown)
                {
                    RotateCam(dx, dy);
                }
                if (MouseRButtonDown)
                {
                    if (Renderer.controllightdir)
                    {
                        Renderer.lightdirx += (dx * camera.Sensitivity);
                        Renderer.lightdiry += (dy * camera.Sensitivity);
                    }
                    else if (Renderer.controltimeofday)
                    {
                        float tod = Renderer.timeofday;
                        tod += (dx - dy) / 30.0f;
                        while (tod >= 24.0f) tod -= 24.0f;
                        while (tod < 0.0f) tod += 24.0f;
                        timecycle.SetTime(tod);
                        Renderer.timeofday = tod;

                        float fv = tod * 60.0f;
                        TimeOfDayTrackBar.Value = (int)fv;
                        UpdateTimeOfDayLabel();
                    }
                }

                UpdateMousePosition(e);
            }
        }

        private void PedsForm_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta != 0)
            {
                camera.MouseZoom(e.Delta);
            }
        }

        private void PedsForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (ActiveControl is TextBox)
            {
                var tb = ActiveControl as TextBox;
                if (!tb.ReadOnly) return; //don't move the camera when typing!
            }
            if (ActiveControl is ComboBox)
            {
                var cb = ActiveControl as ComboBox;
                if (cb.DropDownStyle != ComboBoxStyle.DropDownList) return; //nontypable combobox
            }

            bool enablemove = true;// (!iseditmode) || (MouseLButtonDown && (GrabbedMarker == null) && (GrabbedWidget == null));

            Input.KeyDown(e, enablemove);

            var k = e.KeyCode;
            var kb = Input.keyBindings;
            bool ctrl = Input.CtrlPressed;
            bool shift = Input.ShiftPressed;


            if (!ctrl)
            {
                if (k == kb.MoveSlowerZoomIn)
                {
                    camera.MouseZoom(1);
                }
                if (k == kb.MoveFasterZoomOut)
                {
                    camera.MouseZoom(-1);
                }
            }
        }

        private void PedsForm_KeyUp(object sender, KeyEventArgs e)
        {
            Input.KeyUp(e);

            if (ActiveControl is TextBox)
            {
                var tb = ActiveControl as TextBox;
                if (!tb.ReadOnly) return; //don't move the camera when typing!
            }
            if (ActiveControl is ComboBox)
            {
                var cb = ActiveControl as ComboBox;
                if (cb.DropDownStyle != ComboBoxStyle.DropDownList) return; //non-typable combobox
            }
        }

        private void PedsForm_Deactivate(object sender, EventArgs e)
        {
            //try not to lock keyboard movement if the form loses focus.
            Input.KeyboardStop();
        }

        private void StatsUpdateTimer_Tick(object sender, EventArgs e)
        {
            StatsLabel.Text = Renderer.GetStatusText();

            if (Renderer.timerunning)
            {
                float fv = Renderer.timeofday * 60.0f;
                //TimeOfDayTrackBar.Value = (int)fv;
                UpdateTimeOfDayLabel();
            }
        }

        private void ToolsPanelShowButton_Click(object sender, EventArgs e)
        {
            ToolsPanel.Visible = true;
        }

        private void ToolsPanelHideButton_Click(object sender, EventArgs e)
        {
            ToolsPanel.Visible = false;
        }

        private void ToolsDragPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                toolsPanelResizing = true;
                toolsPanelResizeStartX = e.X + ToolsPanel.Left + ToolsDragPanel.Left;
                toolsPanelResizeStartLeft = ToolsPanel.Left;
                toolsPanelResizeStartRight = ToolsPanel.Right;
            }
        }

        private void ToolsDragPanel_MouseUp(object sender, MouseEventArgs e)
        {
            toolsPanelResizing = false;
        }

        private void ToolsDragPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (toolsPanelResizing)
            {
                int rx = e.X + ToolsPanel.Left + ToolsDragPanel.Left;
                int dx = rx - toolsPanelResizeStartX;
                ToolsPanel.Width = toolsPanelResizeStartRight - toolsPanelResizeStartLeft + dx;
            }
        }

        private void ModelsTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Node != null)
            {
                UpdateSelectionDrawFlags(e.Node);
            }
        }

        private void ModelsTreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node != null)
            {
                e.Node.Checked = !e.Node.Checked;
                //UpdateSelectionDrawFlags(e.Node);
            }
        }

        private void ModelsTreeView_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true; //stops annoying ding sound...
        }

        private void ShadowsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            lock (Renderer.RenderSyncRoot)
            {
                Renderer.shaders.shadows = ShadowsCheckBox.Checked;
            }
        }

        private void ControlLightDirCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.controllightdir = ControlLightDirCheckBox.Checked;
        }

        private void TimeOfDayTrackBar_Scroll(object sender, EventArgs e)
        {
            int v = TimeOfDayTrackBar.Value;
            float fh = v / 60.0f;
            UpdateTimeOfDayLabel();
            lock (Renderer.RenderSyncRoot)
            {
                Renderer.timeofday = fh;
                timecycle.SetTime(Renderer.timeofday);
            }
        }

        private void WireframeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.shaders.wireframe = WireframeCheckBox.Checked;
        }

        private void RenderModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            TextureSamplerComboBox.Enabled = false;
            TextureCoordsComboBox.Enabled = false;
            switch (RenderModeComboBox.Text)
            {
                default:
                case "Default":
                    Renderer.shaders.RenderMode = WorldRenderMode.Default;
                    break;
                case "Single texture":
                    Renderer.shaders.RenderMode = WorldRenderMode.SingleTexture;
                    TextureSamplerComboBox.Enabled = true;
                    TextureCoordsComboBox.Enabled = true;
                    break;
                case "Vertex normals":
                    Renderer.shaders.RenderMode = WorldRenderMode.VertexNormals;
                    break;
                case "Vertex tangents":
                    Renderer.shaders.RenderMode = WorldRenderMode.VertexTangents;
                    break;
                case "Vertex colour 1":
                    Renderer.shaders.RenderMode = WorldRenderMode.VertexColour;
                    Renderer.shaders.RenderVertexColourIndex = 1;
                    break;
                case "Vertex colour 2":
                    Renderer.shaders.RenderMode = WorldRenderMode.VertexColour;
                    Renderer.shaders.RenderVertexColourIndex = 2;
                    break;
                case "Vertex colour 3":
                    Renderer.shaders.RenderMode = WorldRenderMode.VertexColour;
                    Renderer.shaders.RenderVertexColourIndex = 3;
                    break;
                case "Texture coord 1":
                    Renderer.shaders.RenderMode = WorldRenderMode.TextureCoord;
                    Renderer.shaders.RenderTextureCoordIndex = 1;
                    break;
                case "Texture coord 2":
                    Renderer.shaders.RenderMode = WorldRenderMode.TextureCoord;
                    Renderer.shaders.RenderTextureCoordIndex = 2;
                    break;
                case "Texture coord 3":
                    Renderer.shaders.RenderMode = WorldRenderMode.TextureCoord;
                    Renderer.shaders.RenderTextureCoordIndex = 3;
                    break;
            }
        }

        private void TextureSamplerComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (TextureSamplerComboBox.SelectedItem is ShaderParamNames)
            {
                Renderer.shaders.RenderTextureSampler = (ShaderParamNames)TextureSamplerComboBox.SelectedItem;
            }
        }

        private void TextureCoordsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (TextureCoordsComboBox.Text)
            {
                default:
                case "Texture coord 1":
                    Renderer.shaders.RenderTextureSamplerCoord = 1;
                    break;
                case "Texture coord 2":
                    Renderer.shaders.RenderTextureSamplerCoord = 2;
                    break;
                case "Texture coord 3":
                    Renderer.shaders.RenderTextureSamplerCoord = 3;
                    break;
            }
        }

        private void SkeletonsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderskeletons = SkeletonsCheckBox.Checked;
        }

        private void StatusBarCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            StatusStrip.Visible = StatusBarCheckBox.Checked;
        }

        private void ErrorConsoleCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            ConsolePanel.Visible = ErrorConsoleCheckBox.Checked;
        }

        private void PedNameComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!GameFileCache.IsInited) return;

            LoadPed();
        }

        private void ClipDictComboBox_TextChanged(object sender, EventArgs e)
        {
            LoadClipDict(ClipDictComboBox.Text);
        }

        private void ClipComboBox_TextChanged(object sender, EventArgs e)
        {
            SelectClip(ClipComboBox.Text);
        }

        private void EnableRootMotionCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SelectedPed.EnableRootMotion = EnableRootMotionCheckBox.Checked;
        }

        private void HDRRenderingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            lock (Renderer.RenderSyncRoot)
            {
                Renderer.shaders.hdr = HDRRenderingCheckBox.Checked;
            }
        }

        private void EnableAnimationCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            ClipComboBox.Enabled = EnableAnimationCheckBox.Checked;
            ClipDictComboBox.Enabled = EnableAnimationCheckBox.Checked;
            EnableRootMotionCheckBox.Enabled = EnableAnimationCheckBox.Checked;
            PlaybackSpeedTrackBar.Enabled = EnableAnimationCheckBox.Checked;

            if (EnableAnimationCheckBox.Checked)
            {
                LoadAnimsForModel(SelectedPed.Name);
            } 
            else
            {
                ClipDictComboBox.Text = "";
                ClipComboBox.Text = "";
            }
        }

        private void PlaybackSpeedTrackBar_Scroll(object sender, EventArgs e)
        {
            int v = PlaybackSpeedTrackBar.Value;
            float fh = v / 60.0f;
            UpdatePlaybackSpeedLabel();

            lock (Renderer.RenderSyncRoot)
            {
                SelectedPed.AnimClip.PlaybackSpeed = fh;
            }
        }

        private void UpdatePlaybackSpeedLabel()
        {
            int v = PlaybackSpeedTrackBar.Value;
            float fh = v / 60.0f;
            PlaybackSpeedLabel.Text = string.Format("{0:0.00}", fh);
        }

        private void OptionsComponent_UpDown_ValueChanged(object sender, EventArgs e)
        {
            var compId = Convert.ToInt32(((NumericUpDown)sender).Tag);
            var value = Convert.ToInt32(((NumericUpDown)sender).Value);

            SetComponentDrawable(compId, value);
        }

        private void Save_defaultComp_Click(object sender, EventArgs e)
        {
            //ugly as shit but yeah it works

            int index = PedNameComboBox.SelectedIndex;

            var head = Settings.Default.HeadComp.Split(';');
            head[index] = head_updown.Value.ToString();
            Settings.Default.HeadComp = string.Join(";", head);

            var berd = Settings.Default.BerdComp.Split(';');
            berd[index] = berd_updown.Value.ToString();
            Settings.Default.BerdComp = string.Join(";", berd);

            var hair = Settings.Default.HairComp.Split(';');
            hair[index] = hair_updown.Value.ToString();
            Settings.Default.HairComp = string.Join(";", hair);

            var uppr = Settings.Default.UpprComp.Split(';');
            uppr[index] = uppr_updown.Value.ToString();
            Settings.Default.UpprComp = string.Join(";", uppr);

            var lowr = Settings.Default.LowrComp.Split(';');
            lowr[index] = lowr_updown.Value.ToString();
            Settings.Default.LowrComp = string.Join(";", lowr);

            var feet = Settings.Default.FeetComp.Split(';');
            feet[index] = feet_updown.Value.ToString();
            Settings.Default.FeetComp = string.Join(";", feet);



            Settings.Default.Save();
        }

        private void LiveTexturePreview_Click(object sender, EventArgs e)
        {
            if (liveTexturePath != null)
            {
                liveTexturePath = null;
                Renderer.LiveTextureEnabled = false;
            }
            else
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        liveTexturePath = fbd.SelectedPath;
                        Renderer.LiveTextureEnabled = true;
                    }
                }
            }

            liveTxtButton.Text = Renderer.LiveTextureEnabled ? "Disable" : "Enable";
            diffuseRadio.Enabled = !Renderer.LiveTextureEnabled;
            normalRadio.Enabled = !Renderer.LiveTextureEnabled;
            specularRadio.Enabled = !Renderer.LiveTextureEnabled;
        }

        private void liveTexture_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;
            if (radioButton.Checked == true)
            {
                switch (radioButton.Text)
                {
                    case "Diffuse":
                        Renderer.LiveTextureSelectedMode = LiveTextureMode.Diffuse;
                        break;
                    case "Normal":
                        Renderer.LiveTextureSelectedMode = LiveTextureMode.Normal;
                        break;
                    case "Specular":
                        Renderer.LiveTextureSelectedMode = LiveTextureMode.Specular;
                        break;
                }
            }
        }

        private void FloorCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            floorUpDown.Enabled = floorCheckbox.Checked;
        }

        private void FloorUpDown_ValueChanged(object sender, EventArgs e)
        {
            var value = Convert.ToInt32(((NumericUpDown)sender).Value);

        }

        private void AutoRotateTimer_Tick(object sender, EventArgs e)
        {
            if (SelectedPed != null)
            {
                autoRotateAngle += AutoRotateSpeed * (float)Math.PI / 180f;
                if (autoRotateAngle > 2 * Math.PI) // Keep the autoRotateAngle within the 0 to 360
                {
                    autoRotateAngle -= 2 * (float)Math.PI;
                }

                Quaternion newRotation = Quaternion.RotationYawPitchRoll(0, 0, autoRotateAngle);
                SelectedPed.Rotation = newRotation;
                SelectedPed.UpdateEntity();
            }
        }

        private void AutoRotatePedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (AutoRotatePedCheckBox.Checked)
            {
                autoRotateTimer.Start();
            }
            else
            {
                autoRotateTimer.Stop();
            }
        }

        private void OnlySelectedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (OnlySelectedCheckBox.Checked == renderOnlySelected) { return; }
            renderOnlySelected = OnlySelectedCheckBox.Checked;
            
            UpdateModelsUI();
        }

        private void SetDefaultCameraPosition()
        {
            camera.FollowEntity = camEntity;
            camera.FollowEntity.Position = Vector3.Zero;// prevworldpos;

            // used to be Vector3.ForwardLH, but default animations rotates ped, so changed it to Vector3.ForwardRH 
            camera.FollowEntity.Orientation = Quaternion.LookAtLH(Vector3.Zero, Vector3.Up, Vector3.ForwardRH); 

            camera.TargetDistance = 2.0f;
            camera.CurrentDistance = 2.0f;
            camera.TargetRotation.Y = 0.2f;
            camera.CurrentRotation.Y = 0.2f;
            camera.TargetRotation.X = 1.0f * (float)Math.PI;
            camera.CurrentRotation.X = 1.0f * (float)Math.PI;

            if (SelectedPed != null)
            {
                // restart rotation angle, so ped faces camera
                autoRotateAngle = 0f;
                SelectedPed.Rotation = Quaternion.Identity;
                SelectedPed.UpdateEntity();
            }
        }

        private void RestartCamera_Click(object sender, EventArgs e)
        {
            SetDefaultCameraPosition();
        }

        public bool TakeScreenshot(string drawableName, string customFilename = null)
        {
            try
            {
                // Store original states
                originalToolsPanelVisibility = ToolsPanel.Visible;
                originalOnlySelectedState = OnlySelectedCheckBox.Checked;
                originalClearColour = Renderer.DXMan.clearcolour;

                // Set fixed resolution for consistent screenshots
                SetFixedResolution();

                // Configure interface for screenshot
                ToolsPanel.Visible = false; // Hide tools panel
                OnlySelectedCheckBox.Checked = true; // Show only selected drawable
                
                // Enable transparent screenshot mode
                isTransparentScreenshotMode = true;
                
                // Focus camera on selected drawable for optimal framing
                FocusOnSelectedDrawable();
                
                // Give the camera and resolution change a moment to settle
                System.Threading.Thread.Sleep(300);

                // Create screenshot directory if it doesn't exist
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string screenshotDir = Path.Combine(documentsPath, "grzyClothTool", "Screenshots");
                Directory.CreateDirectory(screenshotDir);

                // Generate filename
                string fileName;
                if (!string.IsNullOrEmpty(customFilename))
                {
                    fileName = customFilename;
                }
                else
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    fileName = $"{drawableName}_{timestamp}";
                }
                
                if (!fileName.EndsWith(".png"))
                {
                    fileName += ".png";
                }

                string filePath = Path.Combine(screenshotDir, fileName);

                // Attempt DirectX screenshot first, then fallback to GDI
                bool success = false;
                string method = "Unknown";

                // Try DirectX method first
                try
                {
                    success = TakeDirectXTransparentScreenshot(filePath);
                    if (success)
                    {
                        method = "DirectX";
                    }
                }
                catch (Exception dxEx)
                {
                    LogError($"DirectX screenshot failed: {dxEx.Message}");
                }

                // Fallback to GDI method if DirectX failed
                if (!success)
                {
                    // GDI method implementation would go here if needed
                    // For now, we'll focus on the DirectX method
                    LogError("Screenshot capture failed");
                    return false;
                }

                // Update status with method used and camera positioning info
                UpdateStatus($"Screenshot saved using {method} method at {FIXED_SCREENSHOT_RESOLUTION}x{FIXED_SCREENSHOT_RESOLUTION} with auto-focus: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Screenshot error: {ex.Message}");
                return false;
            }
            finally
            {
                // Always restore original settings
                RestoreOriginalSettings();
            }
        }

        private bool TakeDirectXTransparentScreenshot(string filePath)
        {
            try
            {
                var device = Renderer.DXMan.device;
                var context = Renderer.DXMan.context;
                
                // Instead of capturing from backbuffer, capture from the render target where transparency is handled
                Texture2D sourceTexture = null;
                Texture2DDescription desc;
                
                if (Renderer.shaders?.DefScene?.SceneColour != null)
                {
                    // Use DefScene SceneColour render target - this has our transparent background
                    sourceTexture = Renderer.shaders.DefScene.SceneColour.Texture;
                    desc = sourceTexture.Description;
                }
                else if (Renderer.shaders?.HDR?.PrimaryTexture != null)
                {
                    // Fallback to HDR Primary render target
                    sourceTexture = Renderer.shaders.HDR.PrimaryTexture.Texture;
                    desc = sourceTexture.Description;
                }
                else
                {
                    // Last resort - use backbuffer (but this won't have transparency)
                    sourceTexture = Renderer.DXMan.backbuffer;
                    desc = sourceTexture.Description;
                }

                // Create staging texture for CPU access with alpha support
                var stagingDesc = desc;
                stagingDesc.Usage = ResourceUsage.Staging;
                stagingDesc.BindFlags = BindFlags.None;
                stagingDesc.CpuAccessFlags = CpuAccessFlags.Read;
                stagingDesc.SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0);

                using (var stagingTexture = new SharpDX.Direct3D11.Texture2D(device, stagingDesc))
                {
                    // Handle multisampled source texture
                    if (desc.SampleDescription.Count > 1)
                    {
                        // Create resolved texture first
                        var resolvedDesc = desc;
                        resolvedDesc.SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0);

                        using (var resolvedTexture = new SharpDX.Direct3D11.Texture2D(device, resolvedDesc))
                        {
                            // Resolve multisampling
                            context.ResolveSubresource(sourceTexture, 0, resolvedTexture, 0, desc.Format);
                            // Copy to staging
                            context.CopyResource(resolvedTexture, stagingTexture);
                        }
                    }
                    else
                    {
                        // Direct copy for non-multisampled
                        context.CopyResource(sourceTexture, stagingTexture);
                    }

                    // Map staging texture to read pixels
                    var dataBox = context.MapSubresource(stagingTexture, 0, SharpDX.Direct3D11.MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                    try
                    {
                        return ProcessTransparentScreenshotData(filePath, dataBox, desc.Width, desc.Height);
                    }
                    finally
                    {
                        context.UnmapSubresource(stagingTexture, 0);
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private unsafe bool ProcessTransparentScreenshotData(string filePath, SharpDX.DataBox dataBox, int width, int height)
        {
            try
            {
                using (var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    var bitmapData = bitmap.LockBits(
                        new System.Drawing.Rectangle(0, 0, width, height),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    try
                    {
                        var sourcePtr = dataBox.DataPointer;
                        var destPtr = bitmapData.Scan0;

                        // Process pixels with native transparency support
                        ProcessTransparentPixels(sourcePtr, destPtr, width, height, dataBox.RowPitch, bitmapData.Stride);
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                    }

                    // Auto-crop to fit clothing perfectly
                    using (var finalBitmap = AutoCropToFitClothing(bitmap))
                    {
                        // Save as PNG with alpha channel
                        finalBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private unsafe void ProcessTransparentPixels(IntPtr sourcePtr, IntPtr destPtr, int width, int height, int sourcePitch, int destStride)
        {
            byte* srcBytes = (byte*)sourcePtr;
            byte* dstBytes = (byte*)destPtr;

            for (int y = 0; y < height; y++)
            {
                byte* srcRow = srcBytes + y * sourcePitch;
                byte* dstRow = dstBytes + y * destStride;

                for (int x = 0; x < width; x++)
                {
                    // DirectX SceneColour format is RGBA (R32G32B32A32_Float converted to bytes)
                    byte r = srcRow[x * 4 + 0]; // Red
                    byte g = srcRow[x * 4 + 1]; // Green  
                    byte b = srcRow[x * 4 + 2]; // Blue
                    byte a = srcRow[x * 4 + 3]; // Alpha

                    // When we render with transparent background, the background areas should
                    // already have alpha = 0, and solid objects should have alpha > 0
                    // But DirectX doesn't always handle this perfectly, so we need to check
                    
                    // Check if this pixel matches the transparent clear color exactly
                    bool isTransparentBackground = (r == 0 && g == 0 && b == 0 && a == 0);
                    
                    // Also check for the background color in case transparency didn't work
                    // Original blue background: RGB(51, 102, 153) = (0.2, 0.4, 0.6) * 255
                    bool isBackgroundColor = (Math.Abs(r - 51) <= 5 && Math.Abs(g - 102) <= 5 && Math.Abs(b - 153) <= 5) ||
                                           (Math.Abs(r - 153) <= 5 && Math.Abs(g - 102) <= 5 && Math.Abs(b - 51) <= 5); // Handle R/B swap
                    
                    if (isTransparentBackground || isBackgroundColor)
                    {
                        // Make completely transparent
                        dstRow[x * 4 + 0] = 0; // B
                        dstRow[x * 4 + 1] = 0; // G
                        dstRow[x * 4 + 2] = 0; // R
                        dstRow[x * 4 + 3] = 0; // A
                    }
                    else
                    {
                        // System.Drawing.Bitmap expects BGRA format, so swap R and B from DirectX RGBA
                        dstRow[x * 4 + 0] = b; // B (swapped from position 2)
                        dstRow[x * 4 + 1] = g; // G (same position)
                        dstRow[x * 4 + 2] = r; // R (swapped from position 0)
                        dstRow[x * 4 + 3] = 255; // A - full opacity for clothing
                    }
                }
            }
        }

        private System.Drawing.Bitmap AutoCropToFitClothing(System.Drawing.Bitmap originalBitmap)
        {
            try
            {
                // Find the bounding box of all non-transparent pixels
                int minX = originalBitmap.Width;
                int minY = originalBitmap.Height;
                int maxX = -1;
                int maxY = -1;

                var bitmapData = originalBitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, originalBitmap.Width, originalBitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                try
                {
                    unsafe
                    {
                        byte* pixels = (byte*)bitmapData.Scan0;

                        // Scan all pixels to find the bounding box of opaque content
                        for (int y = 0; y < originalBitmap.Height; y++)
                        {
                            for (int x = 0; x < originalBitmap.Width; x++)
                            {
                                byte* pixel = pixels + y * bitmapData.Stride + x * 4;
                                byte alpha = pixel[3]; // Alpha channel

                                // If pixel is not transparent (alpha > threshold)
                                if (alpha > 10) // Small threshold to handle anti-aliasing
                                {
                                    minX = Math.Min(minX, x);
                                    minY = Math.Min(minY, y);
                                    maxX = Math.Max(maxX, x);
                                    maxY = Math.Max(maxY, y);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    originalBitmap.UnlockBits(bitmapData);
                }

                // Check if we found any content
                if (maxX == -1 || maxY == -1)
                {
                    // No opaque content found, return original
                    return new System.Drawing.Bitmap(originalBitmap);
                }

                // Calculate content dimensions
                int contentWidth = maxX - minX + 1;
                int contentHeight = maxY - minY + 1;

                // Calculate the size of the square needed to contain the content
                int squareSize = Math.Max(contentWidth, contentHeight);

                // Add some padding (10% of the square size, minimum 20 pixels)
                int padding = Math.Max(squareSize / 10, 20);
                squareSize += padding * 2;

                // Calculate center of the content
                int contentCenterX = minX + contentWidth / 2;
                int contentCenterY = minY + contentHeight / 2;

                // Calculate crop position to center the content in the square
                int cropX = contentCenterX - squareSize / 2;
                int cropY = contentCenterY - squareSize / 2;

                // Ensure crop rectangle stays within original image bounds
                cropX = Math.Max(0, Math.Min(cropX, originalBitmap.Width - squareSize));
                cropY = Math.Max(0, Math.Min(cropY, originalBitmap.Height - squareSize));

                // Adjust square size if it extends beyond image bounds
                squareSize = Math.Min(squareSize, Math.Min(originalBitmap.Width - cropX, originalBitmap.Height - cropY));

                // Create the cropped bitmap
                var cropRect = new System.Drawing.Rectangle(cropX, cropY, squareSize, squareSize);
                var croppedBitmap = originalBitmap.Clone(cropRect, originalBitmap.PixelFormat);

                UpdateStatus($"Auto-cropped to {squareSize}x{squareSize} square (content: {contentWidth}x{contentHeight}, padding: {padding}px)");
                
                return croppedBitmap;
            }
            catch (Exception ex)
            {
                LogError($"Auto-crop failed: {ex.Message}");
                // Return original bitmap if cropping fails
                return new System.Drawing.Bitmap(originalBitmap);
            }
        }

        private unsafe void RemoveBackground(IntPtr bitmapData, int width, int height, int stride)
        {
            // Professional background removal for solid backgrounds AND transparent textures
            
            // Sample the background color from the corners (they should be background)
            byte* pixels = (byte*)bitmapData;
            
            // Sample from all four corners and use the most common color
            var corner1 = GetPixelColor(pixels, 0, 0, stride);
            var corner2 = GetPixelColor(pixels, width - 1, 0, stride);
            var corner3 = GetPixelColor(pixels, 0, height - 1, stride);
            var corner4 = GetPixelColor(pixels, width - 1, height - 1, stride);

            // Use corner1 as reference background color (usually the most reliable)
            var bgColor = corner1;

            // PHASE 1: Traditional edge-based flood fill for main background removal
            bool[,] visited = new bool[width, height];
            Queue<(int x, int y)> queue = new Queue<(int, int)>();

            // Start flood fill from all edges
            for (int x = 0; x < width; x++)
            {
                // Top edge
                queue.Enqueue((x, 0));
                // Bottom edge  
                queue.Enqueue((x, height - 1));
            }

            for (int y = 0; y < height; y++)
            {
                // Left edge
                queue.Enqueue((0, y));
                // Right edge
                queue.Enqueue((width - 1, y));
            }

            // Flood fill algorithm for connected background areas
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();

                if (x < 0 || x >= width || y < 0 || y >= height || visited[x, y])
                    continue;

                var currentColor = GetPixelColor(pixels, x, y, stride);
                
                // Check if this pixel matches background color (with tolerance)
                if (!ColorsMatch(currentColor, bgColor, 25)) // 25 = tolerance for anti-aliasing
                    continue;

                visited[x, y] = true;

                // Make this pixel transparent
                SetPixelTransparent(pixels, x, y, stride);

                // Add neighbors to queue
                queue.Enqueue((x + 1, y));
                queue.Enqueue((x - 1, y));
                queue.Enqueue((x, y + 1));
                queue.Enqueue((x, y - 1));
            }

            // PHASE 2: Analyze cloth content for blue colors before removing background colors from interior
            bool clothContainsBlue = AnalyzeClothForBlueContent(pixels, width, height, stride, visited, bgColor);

            // PHASE 3: Handle transparent textures (fishnet, lace, mesh, etc.)
            // Make ALL remaining background-colored pixels transparent, but be more careful if cloth contains blue
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!visited[x, y]) // Only check pixels not already processed by flood fill
                    {
                        var currentColor = GetPixelColor(pixels, x, y, stride);
                        
                        // If cloth contains blue colors, use much stricter matching to avoid removing cloth
                        int tolerance = clothContainsBlue ? 8 : 15; // Much stricter if cloth has blue
                        
                        // Also, if cloth contains blue, add additional checks to ensure we're not removing cloth colors
                        if (clothContainsBlue && IsLikelyClothBlue(currentColor, bgColor))
                        {
                            // Skip this pixel - it's likely part of the cloth design
                            continue;
                        }
                        
                        // Use stricter tolerance for internal transparency to avoid removing clothing
                        if (ColorsMatch(currentColor, bgColor, tolerance))
                        {
                            SetPixelTransparent(pixels, x, y, stride);
                        }
                    }
                }
            }
        }

        private unsafe bool AnalyzeClothForBlueContent(byte* pixels, int width, int height, int stride, bool[,] visited, (byte r, byte g, byte b) bgColor)
        {
            int totalClothPixels = 0;
            int blueClothPixels = 0;
            
            // Sample the cloth (non-transparent, non-background pixels) to see if blue is prominent
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!visited[x, y]) // Not background/transparent
                    {
                        var currentColor = GetPixelColor(pixels, x, y, stride);
                        
                        // Skip if this is clearly background color
                        if (ColorsMatch(currentColor, bgColor, 15))
                            continue;
                            
                        totalClothPixels++;
                        
                        // Check if this pixel has significant blue content
                        // A pixel is considered "blue" if:
                        // 1. Blue channel is higher than red and green channels
                        // 2. Blue channel is above a threshold
                        // 3. The color is not too close to the background blue
                        if (IsBlueishClothColor(currentColor, bgColor))
                        {
                            blueClothPixels++;
                        }
                    }
                }
            }
            
            // If more than 10% of cloth pixels contain blue, consider the cloth to have blue content
            if (totalClothPixels > 0)
            {
                double blueRatio = (double)blueClothPixels / totalClothPixels;
                return blueRatio > 0.1; // 10% threshold
            }
            
            return false;
        }
        
        private bool IsBlueishClothColor((byte r, byte g, byte b) color, (byte r, byte g, byte b) bgColor)
        {
            // Check if this color has significant blue content that's different from background
            // Don't consider it blue cloth if it's too similar to the background color
            if (ColorsMatch(color, bgColor, 20))
                return false;
                
            // Must have blue as the dominant or co-dominant color channel
            if (color.b < color.r - 20 && color.b < color.g - 20)
                return false;
                
            // Must have sufficient blue intensity
            if (color.b < 80) // Minimum blue threshold
                return false;
                
            return true;
        }
        
        private bool IsLikelyClothBlue((byte r, byte g, byte b) color, (byte r, byte g, byte b) bgColor)
        {
            // This method determines if a blue-ish color is likely part of the cloth design
            // rather than background bleeding through
            
            // If the color is very close to background, it's probably background
            if (ColorsMatch(color, bgColor, 10))
                return false;
                
            // If blue is dominant and the color is saturated enough, it's likely cloth
            if (IsBlueishClothColor(color, bgColor))
            {
                // Additional check: if the color has good saturation/contrast, it's likely a deliberate color choice
                int maxChannel = Math.Max(Math.Max(color.r, color.g), color.b);
                int minChannel = Math.Min(Math.Min(color.r, color.g), color.b);
                int contrast = maxChannel - minChannel;
                
                // If there's good contrast between channels, it's likely a deliberate color choice
                if (contrast > 30)
                    return true;
            }
            
            return false;
        }

        private unsafe (byte r, byte g, byte b) GetPixelColor(byte* pixels, int x, int y, int stride)
        {
            byte* pixel = pixels + y * stride + x * 4;
            return (pixel[0], pixel[1], pixel[2]); // RGB after our color correction
        }

        private unsafe void SetPixelTransparent(byte* pixels, int x, int y, int stride)
        {
            byte* pixel = pixels + y * stride + x * 4;
            pixel[0] = 0; // R
            pixel[1] = 0; // G
            pixel[2] = 0; // B
            pixel[3] = 0; // A
        }

        private bool ColorsMatch((byte r, byte g, byte b) color1, (byte r, byte g, byte b) color2, int tolerance)
        {
            return Math.Abs(color1.r - color2.r) <= tolerance &&
                   Math.Abs(color1.g - color2.g) <= tolerance &&
                   Math.Abs(color1.b - color2.b) <= tolerance;
        }

        private void RestoreOriginalSettings()
        {
            try
            {
                // Restore transparent background
                if (isTransparentScreenshotMode)
                {
                    SetTransparentBackground(false);
                    isTransparentScreenshotMode = false;
                }

                // Restore tools panel visibility
                ToolsPanel.Visible = originalToolsPanelVisibility;
                
                // Restore only selected state
                OnlySelectedCheckBox.Checked = originalOnlySelectedState;
                
                // Restore original resolution if it was changed
                RestoreOriginalResolution();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error restoring settings: {ex.Message}");
            }
        }

        private void SetTransparentBackground(bool transparent)
        {
            isTransparentRenderingMode = transparent;
            
            if (transparent)
            {
                // Set the clear color to transparent
                var transparentColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
                Renderer.DXMan.SetClearColour(transparentColor);
                
                // Enable transparent background in existing rendering pipeline components
                if (Renderer.shaders?.DefScene != null)
                {
                    Renderer.shaders.DefScene.UseTransparentBackground = true;
                }
                
                if (Renderer.shaders?.HDR != null)
                {
                    Renderer.shaders.HDR.UseTransparentBackground = true;
                }
                
                // Note: If DefScene or HDR are null, they will be created later by ShaderManager.BeginFrame()
                // and we'll need to set their transparent flags at that time
            }
            else
            {
                // Restore original clear color
                Renderer.DXMan.SetClearColour(originalClearColour);
                
                // Disable transparent background in rendering pipeline components
                if (Renderer.shaders?.DefScene != null)
                {
                    Renderer.shaders.DefScene.UseTransparentBackground = false;
                }
                
                if (Renderer.shaders?.HDR != null)
                {
                    Renderer.shaders.HDR.UseTransparentBackground = false;
                }
            }
        }
        


        public void FocusOnSelectedDrawable()
        {
            try
            {
                // Set fixed resolution for consistent camera positioning
                SetFixedResolution();
                
                // First try to get from loaded drawables (from clothing tool)
                Drawable targetDrawable = null;
                Vector3 boundingCenter = Vector3.Zero;
                float boundingSphereRadius = 1.0f;

                if (LoadedDrawables.Count > 0)
                {
                    // Use the first loaded drawable (most recently selected from cloth tool)
                    targetDrawable = LoadedDrawables.Values.FirstOrDefault();
                }
                else if (Renderer.SelectedDrawable != null)
                {
                    // Fallback to renderer's selected drawable
                    targetDrawable = Renderer.SelectedDrawable as Drawable;
                }
                
                if (targetDrawable != null)
                {
                    boundingCenter = targetDrawable.BoundingCenter;
                    boundingSphereRadius = targetDrawable.BoundingSphereRadius;
                }
                else
                {
                    // If no specific drawable, focus on the overall ped with some default positioning
                    boundingCenter = Vector3.Zero;
                    boundingSphereRadius = 2.0f;
                    UpdateStatus("No specific drawable selected, focusing on default position");
                    MoveCameraToView(boundingCenter, boundingSphereRadius);
                    
                    // Restore resolution after a short delay
                    Task.Delay(1000).ContinueWith(_ => RestoreOriginalResolution());
                    return;
                }

                // Adjust for different clothing types - some clothing items are positioned differently
                Vector3 adjustedCenter = boundingCenter;
                
                // Apply a small vertical offset to center clothing items better
                adjustedCenter.Z += boundingSphereRadius * 0.1f;

                // Ensure minimum radius for very small items
                float adjustedRadius = Math.Max(boundingSphereRadius, 0.5f);

                // Move camera to optimal viewing position
                MoveCameraToView(adjustedCenter, adjustedRadius);

                // Update status
                UpdateStatus($"Camera focused on clothing item: {targetDrawable?.Name ?? "Unknown"} at {FIXED_SCREENSHOT_RESOLUTION}x{FIXED_SCREENSHOT_RESOLUTION} (Center: {adjustedCenter:F2}, Radius: {adjustedRadius:F2})");
                
                // Restore resolution after a short delay to allow viewing
                Task.Delay(1000).ContinueWith(_ => RestoreOriginalResolution());
            }
            catch (Exception ex)
            {
                LogError($"Error focusing camera on drawable: {ex.Message}");
                // Fallback to default camera position
                SetDefaultCameraPosition();
                // Restore resolution in case of error
                RestoreOriginalResolution();
            }
        }

        private void SetFixedResolution()
        {
            try
            {
                if (isFixedResolutionMode) return; // Already in fixed mode

                // Store original resolution
                originalWidth = ClientSize.Width;
                originalHeight = ClientSize.Height;
                isFixedResolutionMode = true;

                // Set form to fixed resolution
                Size = new System.Drawing.Size(FIXED_SCREENSHOT_RESOLUTION + (Width - ClientSize.Width), 
                                             FIXED_SCREENSHOT_RESOLUTION + (Height - ClientSize.Height));
                
                // Force render buffers to resize
                Renderer.BuffersResized(FIXED_SCREENSHOT_RESOLUTION, FIXED_SCREENSHOT_RESOLUTION);
                
                UpdateStatus($"Set fixed resolution: {FIXED_SCREENSHOT_RESOLUTION}x{FIXED_SCREENSHOT_RESOLUTION}");
            }
            catch (Exception ex)
            {
                LogError($"Error setting fixed resolution: {ex.Message}");
            }
        }

        private void RestoreOriginalResolution()
        {
            try
            {
                if (!isFixedResolutionMode) return; // Not in fixed mode

                // Restore original size
                Size = new System.Drawing.Size(originalWidth + (Width - ClientSize.Width), 
                                             originalHeight + (Height - ClientSize.Height));
                
                // Force render buffers to resize back to original
                Renderer.BuffersResized(originalWidth, originalHeight);
                
                isFixedResolutionMode = false;
                UpdateStatus("Restored original resolution");
            }
            catch (Exception ex)
            {
                LogError($"Error restoring resolution: {ex.Message}");
            }
        }
    }
}
