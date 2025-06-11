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
using System.Drawing;
using System.Drawing.Imaging;

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

        /// <summary>
        /// Alpha mask cache to store masks for each cloth (not per texture variation)
        /// </summary>
        private static Dictionary<string, System.Drawing.Bitmap> alphaMaskCache = new Dictionary<string, System.Drawing.Bitmap>();

        /// <summary>
        /// Cropping parameters cache to store the bounding box for each cloth
        /// </summary>
        private static Dictionary<string, System.Drawing.Rectangle> cropParametersCache = new Dictionary<string, System.Drawing.Rectangle>();

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

        /// <summary>
        /// Generates an alpha mask for a cloth by rendering in Vertex Colour 2 mode and removing blue color
        /// </summary>
        /// <param name="clothName">Name of the cloth to generate mask for</param>
        /// <returns>Alpha mask bitmap, or null if failed</returns>
        public System.Drawing.Bitmap GenerateAlphaMask(string clothName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== GenerateAlphaMask START for: {clothName} ===");
                
                // Check if mask already exists in cache
                if (alphaMaskCache.ContainsKey(clothName))
                {
                    System.Diagnostics.Debug.WriteLine($"Alpha mask found in cache for {clothName}");
                    return alphaMaskCache[clothName];
                }
                
                // Store current render mode
                string originalRenderMode = RenderModeComboBox.Text;
                System.Diagnostics.Debug.WriteLine($"Current render mode: {originalRenderMode}");
                
                // Switch to Vertex Colour 2 mode
                RenderModeComboBox.Text = "Vertex colour 2";
                System.Diagnostics.Debug.WriteLine("Switched to Vertex Colour 2 mode");
                
                // Longer delay to ensure render mode change takes effect completely
                System.Threading.Thread.Sleep(200); // Increased from 100ms
                this.Refresh(); // Force a refresh to ensure mode change is applied
                System.Threading.Thread.Sleep(100); // Additional delay after refresh
                System.Diagnostics.Debug.WriteLine("Render mode switch should be complete");
                
                // Capture the screen area for mask generation
                System.Drawing.Rectangle clientBounds = this.ClientRectangle;
                System.Drawing.Bitmap maskBitmap = new System.Drawing.Bitmap(clientBounds.Width, clientBounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(maskBitmap))
                {
                    System.Drawing.Point clientLocation = this.PointToScreen(clientBounds.Location);
                    graphics.CopyFromScreen(clientLocation, System.Drawing.Point.Empty, clientBounds.Size);
                }
                
                // Crop the mask using the same parameters as regular screenshots
                int cropTop = 0;         
                int cropLeft = 58;       
                int cropBottom = 26;     
                int cropRight = 0;       
                
                int finalWidth = Math.Max(1, maskBitmap.Width - cropLeft - cropRight);
                int finalHeight = Math.Max(1, maskBitmap.Height - cropTop - cropBottom);
                
                System.Drawing.Bitmap croppedMask = new System.Drawing.Bitmap(finalWidth, finalHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                
                using (System.Drawing.Graphics cropGraphics = System.Drawing.Graphics.FromImage(croppedMask))
                {
                    System.Drawing.Rectangle sourceRect = new System.Drawing.Rectangle(cropLeft, cropTop, finalWidth, finalHeight);
                    System.Drawing.Rectangle destRect = new System.Drawing.Rectangle(0, 0, finalWidth, finalHeight);
                    cropGraphics.DrawImage(maskBitmap, destRect, sourceRect, System.Drawing.GraphicsUnit.Pixel);
                }
                
                // Convert blue areas to alpha mask (blue = transparent, non-blue = opaque)
                CreateAlphaMaskFromBlue(croppedMask, System.Drawing.Color.FromArgb(51, 102, 153), 3);
                
                // Find the smallest bounding box with 2px padding
                System.Drawing.Rectangle contentBounds = FindContentBounds(croppedMask, 2);
                System.Diagnostics.Debug.WriteLine($"Content bounds with 2px padding: {contentBounds}");
                
                // Crop the alpha mask to the content bounds
                System.Drawing.Bitmap finalMask = CropBitmap(croppedMask, contentBounds);
                
                // Store both the mask and crop parameters in cache
                alphaMaskCache[clothName] = (System.Drawing.Bitmap)finalMask.Clone();
                cropParametersCache[clothName] = contentBounds;
                
                System.Diagnostics.Debug.WriteLine($"Alpha mask cropped to {finalMask.Width}x{finalMask.Height} and cached");
                
                // Restore original render mode for textured screenshots
                RenderModeComboBox.Text = originalRenderMode;
                System.Diagnostics.Debug.WriteLine($"Restored render mode to: {originalRenderMode} for textured screenshots");
                
                // CRITICAL: Wait for render mode to switch back completely
                System.Threading.Thread.Sleep(200); // Ensure mode switch completes
                this.Refresh(); // Force refresh to apply mode change
                System.Threading.Thread.Sleep(100); // Additional delay after refresh
                System.Diagnostics.Debug.WriteLine($"Render mode switch back to {originalRenderMode} should be complete");
                
                System.Diagnostics.Debug.WriteLine($"=== GenerateAlphaMask END SUCCESS for: {clothName} ===");
                
                maskBitmap.Dispose();
                croppedMask.Dispose(); // Dispose the intermediate image
                return finalMask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateAlphaMask exception for {clothName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts blue areas in a bitmap to alpha mask data
        /// Blue areas become transparent (alpha = 0), non-blue areas become opaque (alpha = 255)
        /// </summary>
        /// <param name="bitmap">The bitmap to process</param>
        /// <param name="blueColor">The blue color to detect</param>
        /// <param name="threshold">Color matching threshold</param>
        private void CreateAlphaMaskFromBlue(System.Drawing.Bitmap bitmap, System.Drawing.Color blueColor, int threshold)
        {
            try
            {
                System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadWrite,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* ptr = (byte*)bitmapData.Scan0.ToPointer();
                    int bytesPerPixel = 4;
                    
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            int index = (y * bitmapData.Stride) + (x * bytesPerPixel);
                            
                            byte blue = ptr[index];
                            byte green = ptr[index + 1];
                            byte red = ptr[index + 2];
                            
                            // Check if this pixel is blue (the background color we want to mask out)
                            if (IsColorSimilar(red, green, blue, blueColor.R, blueColor.G, blueColor.B, threshold))
                            {
                                // Blue areas become transparent in the mask
                                ptr[index] = 0;     // Blue = 0
                                ptr[index + 1] = 0; // Green = 0
                                ptr[index + 2] = 0; // Red = 0
                                ptr[index + 3] = 0; // Alpha = 0 (transparent)
                            }
                            else
                            {
                                // Non-blue areas become opaque white in the mask
                                ptr[index] = 255;     // Blue = 255
                                ptr[index + 1] = 255; // Green = 255
                                ptr[index + 2] = 255; // Red = 255
                                ptr[index + 3] = 255; // Alpha = 255 (opaque)
                            }
                        }
                    }
                }

                bitmap.UnlockBits(bitmapData);
                System.Diagnostics.Debug.WriteLine("Alpha mask creation completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateAlphaMaskFromBlue exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies an alpha mask to a bitmap and crops both to match
        /// Where mask is transparent, the target becomes transparent
        /// Where mask is opaque, the target remains unchanged
        /// </summary>
        /// <param name="targetBitmap">The bitmap to apply the mask to</param>
        /// <param name="alphaMask">The alpha mask to apply</param>
        /// <param name="clothName">Cloth name to get crop parameters</param>
        /// <returns>Cropped bitmap with alpha mask applied</returns>
        private System.Drawing.Bitmap ApplyAlphaMaskAndCrop(System.Drawing.Bitmap targetBitmap, System.Drawing.Bitmap alphaMask, string clothName)
        {
            try
            {
                // Get the crop parameters for this cloth
                System.Drawing.Rectangle cropArea = new System.Drawing.Rectangle(0, 0, targetBitmap.Width, targetBitmap.Height);
                if (cropParametersCache.ContainsKey(clothName))
                {
                    cropArea = cropParametersCache[clothName];
                    System.Diagnostics.Debug.WriteLine($"Using cached crop parameters for {clothName}: {cropArea}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No crop parameters found for {clothName}, using full image");
                }
                
                // First crop the target bitmap to match the alpha mask
                System.Drawing.Bitmap croppedTarget = CropBitmap(targetBitmap, cropArea);
                
                // Ensure both bitmaps are the same size after cropping
                if (croppedTarget.Width != alphaMask.Width || croppedTarget.Height != alphaMask.Height)
                {
                    System.Diagnostics.Debug.WriteLine($"Size mismatch after cropping: target={croppedTarget.Width}x{croppedTarget.Height}, mask={alphaMask.Width}x{alphaMask.Height}");
                    // Try to resize to match if there's a small difference
                    if (Math.Abs(croppedTarget.Width - alphaMask.Width) <= 1 && Math.Abs(croppedTarget.Height - alphaMask.Height) <= 1)
                    {
                        System.Diagnostics.Debug.WriteLine("Small size difference, creating new bitmap with mask dimensions");
                        System.Drawing.Bitmap resizedTarget = new System.Drawing.Bitmap(alphaMask.Width, alphaMask.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(resizedTarget))
                        {
                            g.DrawImage(croppedTarget, 0, 0, alphaMask.Width, alphaMask.Height);
                        }
                        croppedTarget.Dispose();
                        croppedTarget = resizedTarget;
                    }
                    else
                    {
                        return croppedTarget; // Return without applying mask if sizes don't match
                    }
                }
                
                // Apply the alpha mask
                System.Drawing.Imaging.BitmapData targetData = croppedTarget.LockBits(
                    new System.Drawing.Rectangle(0, 0, croppedTarget.Width, croppedTarget.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadWrite,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    
                System.Drawing.Imaging.BitmapData maskData = alphaMask.LockBits(
                    new System.Drawing.Rectangle(0, 0, alphaMask.Width, alphaMask.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* targetPtr = (byte*)targetData.Scan0.ToPointer();
                    byte* maskPtr = (byte*)maskData.Scan0.ToPointer();
                    int bytesPerPixel = 4;
                    
                    for (int y = 0; y < croppedTarget.Height; y++)
                    {
                        for (int x = 0; x < croppedTarget.Width; x++)
                        {
                            int index = (y * targetData.Stride) + (x * bytesPerPixel);
                            int maskIndex = (y * maskData.Stride) + (x * bytesPerPixel);
                            
                            byte maskAlpha = maskPtr[maskIndex + 3];
                            
                            // If mask is transparent (alpha = 0), make target transparent
                            // If mask is opaque (alpha = 255), keep target as is
                            targetPtr[index + 3] = maskAlpha;
                        }
                    }
                }

                croppedTarget.UnlockBits(targetData);
                alphaMask.UnlockBits(maskData);
                
                System.Diagnostics.Debug.WriteLine($"Alpha mask applied and cropped to {croppedTarget.Width}x{croppedTarget.Height}");
                return croppedTarget;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyAlphaMaskAndCrop exception: {ex.Message}");
                return (System.Drawing.Bitmap)targetBitmap.Clone();
            }
        }

        /// <summary>
        /// Enhanced screenshot method using alpha mask for cleaner results
        /// For each cloth, generates alpha mask once in Vertex Colour 2 mode, then applies it to all texture variations
        /// </summary>
        /// <param name="filePath">Path to save the screenshot</param>
        /// <param name="clothName">Name of the cloth (used for alpha mask caching)</param>
        /// <param name="useAlphaMask">Whether to use alpha mask (true) or direct blue removal (false)</param>
        /// <param name="renderResolution">Window resolution during capture (e.g., "1024x1024")</param>
        /// <param name="outputResolution">Final image resolution (e.g., "128x128")</param>
        /// <returns>True if successful</returns>
        public bool TakeGDIScreenshotWithAlphaMask(string filePath, string clothName = null, bool useAlphaMask = true, string renderResolution = null, string outputResolution = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== TakeGDIScreenshotWithAlphaMask START ===");
                System.Diagnostics.Debug.WriteLine($"FilePath: {filePath}");
                System.Diagnostics.Debug.WriteLine($"ClothName: {clothName}");
                System.Diagnostics.Debug.WriteLine($"UseAlphaMask: {useAlphaMask}");
                System.Diagnostics.Debug.WriteLine($"RenderResolution: {renderResolution}");
                System.Diagnostics.Debug.WriteLine($"OutputResolution: {outputResolution}");
                
                // Enable double-sided rendering for better screenshot quality
                EnableDoubleSidedRendering();
                
                // Check if this is the first texture (index 0) - generate alpha mask for this cloth
                bool isFirstTexture = !string.IsNullOrEmpty(filePath) && filePath.EndsWith("_0.png");
                
                if (isFirstTexture && useAlphaMask && !string.IsNullOrEmpty(clothName))
                {
                    System.Diagnostics.Debug.WriteLine("🔄 FIRST TEXTURE: Generating alpha mask while cloth is visible");
                    
                    // Generate alpha mask NOW while the cloth is properly loaded and visible
                    System.Drawing.Bitmap alphaMask = GenerateAlphaMask(clothName);
                    if (alphaMask != null)
                    {
                        alphaMask.Dispose(); // We only need it cached, not returned
                        System.Diagnostics.Debug.WriteLine("✓ Alpha mask generated and cached for cloth while visible");
                        
                        // CRITICAL: Wait for render mode to fully switch back to textured mode
                        System.Diagnostics.Debug.WriteLine("⏳ Waiting for render mode to switch back to textured mode...");
                        System.Threading.Thread.Sleep(200); // Increased delay to ensure mode switch completes
                        
                        // Force a refresh to ensure the textured view is properly rendered
                        this.Refresh();
                        System.Threading.Thread.Sleep(100); // Additional delay after refresh
                        
                        System.Diagnostics.Debug.WriteLine("✓ Render mode should now be back to textured mode");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("❌ Failed to generate alpha mask");
                        useAlphaMask = false;
                    }
                }
                
                // Continue with regular screenshot capture for ALL textures (including index 0)
                System.Drawing.Rectangle clientBounds = this.ClientRectangle;
                System.Drawing.Bitmap fullBitmap = new System.Drawing.Bitmap(clientBounds.Width, clientBounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(fullBitmap))
                {
                    System.Drawing.Point clientLocation = this.PointToScreen(clientBounds.Location);
                    graphics.CopyFromScreen(clientLocation, System.Drawing.Point.Empty, clientBounds.Size);
                }
                
                // Apply cropping
                int cropTop = 0;         
                int cropLeft = 58;       
                int cropBottom = 26;     
                int cropRight = 0;       
                
                int finalWidth = Math.Max(1, fullBitmap.Width - cropLeft - cropRight);
                int finalHeight = Math.Max(1, fullBitmap.Height - cropTop - cropBottom);
                
                using (System.Drawing.Bitmap processedBitmap = new System.Drawing.Bitmap(finalWidth, finalHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (System.Drawing.Graphics cropGraphics = System.Drawing.Graphics.FromImage(processedBitmap))
                    {
                        System.Drawing.Rectangle sourceRect = new System.Drawing.Rectangle(cropLeft, cropTop, finalWidth, finalHeight);
                        System.Drawing.Rectangle destRect = new System.Drawing.Rectangle(0, 0, finalWidth, finalHeight);
                        cropGraphics.DrawImage(fullBitmap, destRect, sourceRect, System.Drawing.GraphicsUnit.Pixel);
                    }
                    
                    // Apply transparency processing
                    System.Drawing.Bitmap finalBitmap;
                    
                    System.Diagnostics.Debug.WriteLine($"=== ALPHA MASK APPLICATION DEBUG ===");
                    System.Diagnostics.Debug.WriteLine($"useAlphaMask: {useAlphaMask}");
                    System.Diagnostics.Debug.WriteLine($"clothName: '{clothName}'");
                    System.Diagnostics.Debug.WriteLine($"alphaMaskCache.Count: {alphaMaskCache.Count}");
                    
                    if (!string.IsNullOrEmpty(clothName))
                    {
                        System.Diagnostics.Debug.WriteLine($"alphaMaskCache.ContainsKey('{clothName}'): {alphaMaskCache.ContainsKey(clothName)}");
                        if (alphaMaskCache.ContainsKey(clothName))
                        {
                            var cachedMask = alphaMaskCache[clothName];
                            System.Diagnostics.Debug.WriteLine($"Cached alpha mask size: {cachedMask?.Width}x{cachedMask?.Height}");
                        }
                        
                        // List all keys in cache for debugging
                        System.Diagnostics.Debug.WriteLine("All cache keys:");
                        foreach (var key in alphaMaskCache.Keys)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - '{key}'");
                        }
                    }
                    
                    if (useAlphaMask && alphaMaskCache.ContainsKey(clothName))
                    {
                        // Use alpha mask approach with auto-cropping
                        System.Diagnostics.Debug.WriteLine("✓ APPLYING ALPHA MASK with auto-cropping");
                        finalBitmap = ApplyAlphaMaskAndCrop(processedBitmap, alphaMaskCache[clothName], clothName);
                        
                        // Remove any remaining blue pixels if they're minority (<30%)
                        System.Diagnostics.Debug.WriteLine("✓ CLEANING UP remaining blue pixels");
                        int bluePixelsRemoved = RemoveBluePixelsIfMinority(finalBitmap, System.Drawing.Color.FromArgb(51, 102, 153), 3);
                        if (bluePixelsRemoved == -1)
                        {
                            System.Diagnostics.Debug.WriteLine("Blue pixel removal skipped - likely blue clothing");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Removed {bluePixelsRemoved} remaining blue pixels");
                        }
                    }
                    else
                    {
                        // Use direct blue removal approach
                        System.Diagnostics.Debug.WriteLine("⚠️ USING DIRECT BLUE REMOVAL (no alpha mask)");
                        if (!useAlphaMask)
                        {
                            System.Diagnostics.Debug.WriteLine("Reason: useAlphaMask is false");
                        }
                        else if (string.IsNullOrEmpty(clothName))
                        {
                            System.Diagnostics.Debug.WriteLine("Reason: clothName is null or empty");
                        }
                        else if (!alphaMaskCache.ContainsKey(clothName))
                        {
                            System.Diagnostics.Debug.WriteLine($"Reason: No alpha mask found for '{clothName}'");
                        }
                        
                        RemoveColorAndMakeTransparent(processedBitmap, System.Drawing.Color.FromArgb(51, 102, 153), 3);
                        finalBitmap = processedBitmap;
                    }
                    
                    // Save the final image
                    string directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Apply output resolution scaling if specified
                    System.Drawing.Bitmap finalImageToSave = finalBitmap;
                    if (!string.IsNullOrEmpty(outputResolution))
                    {
                        finalImageToSave = ScaleToOutputResolution(finalBitmap, outputResolution);
                        System.Diagnostics.Debug.WriteLine($"Applied output resolution scaling to {outputResolution}");
                    }
                    
                    finalImageToSave.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    System.Diagnostics.Debug.WriteLine($"Saved final image: {filePath}");
                    
                    // Clean up
                    if (finalImageToSave != finalBitmap)
                    {
                        finalImageToSave.Dispose();
                    }
                    if (finalBitmap != processedBitmap)
                    {
                        finalBitmap.Dispose();
                    }
                    
                    bool success = File.Exists(filePath) && new FileInfo(filePath).Length > 0;
                    System.Diagnostics.Debug.WriteLine($"=== TakeGDIScreenshotWithAlphaMask END: {success} ===");
                    
                    fullBitmap.Dispose();
                    
                    // Restore original rendering settings
                    RestoreOriginalRenderingSettings();
                    
                    return success;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TakeGDIScreenshotWithAlphaMask exception: {ex.Message}");
                
                // Ensure rendering settings are restored even on exception
                RestoreOriginalRenderingSettings();
                
                return false;
            }
        }

        public bool TakeGDIScreenshot(string filePath)
        {
            // FORCE WRITE A DEBUG FILE TO CONFIRM THIS METHOD IS CALLED
            try
            {
                string debugPath = Path.Combine(Path.GetDirectoryName(filePath), "METHOD_CALLED_DEBUG.txt");
                File.WriteAllText(debugPath, $"TakeGDIScreenshot called at {DateTime.Now} for {filePath}");
            }
            catch { }
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== TakeGDIScreenshot START for: {filePath} ===");
                
                // Enable double-sided rendering for better screenshot quality
                EnableDoubleSidedRendering();
                
                // Ensure the form is visible and active
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
                    System.Diagnostics.Debug.WriteLine("Form was minimized, restored to normal");
                }
                
                this.Activate();
                System.Threading.Thread.Sleep(100); // Increased delay for better stability
                System.Diagnostics.Debug.WriteLine("Form activated and delay completed");
                
                // Get the client area bounds (exclude title bar, borders, tools panel)
                System.Drawing.Rectangle clientBounds = this.ClientRectangle;
                System.Diagnostics.Debug.WriteLine($"Original client bounds: {clientBounds}");
                
                // Adjust bounds to exclude the tools panel if visible
                if (ToolsPanel != null && ToolsPanel.Visible)
                {
                    System.Diagnostics.Debug.WriteLine($"Tools panel is visible, width: {ToolsPanel.Width}");
                    clientBounds.Width -= ToolsPanel.Width;
                }
                
                // Adjust bounds to exclude console panel if visible  
                if (ConsolePanel != null && ConsolePanel.Visible)
                {
                    System.Diagnostics.Debug.WriteLine($"Console panel is visible, height: {ConsolePanel.Height}");
                    clientBounds.Height -= ConsolePanel.Height;
                }
                
                System.Diagnostics.Debug.WriteLine($"Adjusted client bounds: {clientBounds}");
                
                if (clientBounds.Width <= 0 || clientBounds.Height <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Invalid client bounds after adjustment");
                    return false;
                }
                
                // Create bitmap for the full screenshot first
                using (System.Drawing.Bitmap fullBitmap = new System.Drawing.Bitmap(clientBounds.Width, clientBounds.Height))
                {
                    System.Diagnostics.Debug.WriteLine($"Created full bitmap: {fullBitmap.Width}x{fullBitmap.Height}");
                    
                    using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(fullBitmap))
                    {
                        // Capture the client area content
                        System.Drawing.Point clientLocation = this.PointToScreen(clientBounds.Location);
                        System.Diagnostics.Debug.WriteLine($"Screen capture location: {clientLocation}");
                        graphics.CopyFromScreen(clientLocation, System.Drawing.Point.Empty, clientBounds.Size);
                        System.Diagnostics.Debug.WriteLine("Screen capture completed");
                    }
                    

                    
                                         // Define crop parameters to remove unwanted areas - USER CALCULATED VALUES
                     int cropTop = 0;         // No top cropping needed
                     int cropLeft = 58;       // Remove left edge - user calculated
                     int cropBottom = 26;     // Remove bottom status bar - user calculated  
                     int cropRight = 0;       // No right cropping needed
                    
                    System.Diagnostics.Debug.WriteLine($"CONSERVATIVE Crop parameters - Top: {cropTop}, Left: {cropLeft}, Bottom: {cropBottom}, Right: {cropRight}");
                    
                    // Calculate the final cropped dimensions (what will remain after cropping)
                    int finalWidth = Math.Max(1, fullBitmap.Width - cropLeft - cropRight);
                    int finalHeight = Math.Max(1, fullBitmap.Height - cropTop - cropBottom);
                    
                    System.Diagnostics.Debug.WriteLine($"Full bitmap: {fullBitmap.Width}x{fullBitmap.Height}");
                    System.Diagnostics.Debug.WriteLine($"Final dimensions after cropping: {finalWidth}x{finalHeight}");
                    
                    if (finalWidth <= 0 || finalHeight <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: Invalid final dimensions");
                        return false;
                    }
                    
                    // Create the final cropped and processed bitmap
                    using (System.Drawing.Bitmap processedBitmap = new System.Drawing.Bitmap(finalWidth, finalHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        System.Diagnostics.Debug.WriteLine($"Created processed bitmap: {processedBitmap.Width}x{processedBitmap.Height}");
                        
                        using (System.Drawing.Graphics cropGraphics = System.Drawing.Graphics.FromImage(processedBitmap))
                        {
                            // Define the source rectangle (the area we want to copy from the original)
                            // Start at (cropLeft, cropTop) and take (finalWidth, finalHeight) pixels
                            System.Drawing.Rectangle sourceRect = new System.Drawing.Rectangle(
                                cropLeft,           // Start X position 
                                cropTop,            // Start Y position 
                                finalWidth,         // Width of the area to copy (already calculated correctly)
                                finalHeight         // Height of the area to copy (already calculated correctly)
                            );
                            
                            // Validate source rectangle bounds
                            if (sourceRect.X < 0 || sourceRect.Y < 0 || 
                                sourceRect.Right > fullBitmap.Width || sourceRect.Bottom > fullBitmap.Height)
                            {
                                System.Diagnostics.Debug.WriteLine($"ERROR: Source rectangle {sourceRect} exceeds bitmap bounds {fullBitmap.Width}x{fullBitmap.Height}");
                                System.Diagnostics.Debug.WriteLine($"sourceRect.Right = {sourceRect.Right}, sourceRect.Bottom = {sourceRect.Bottom}");
                                return false;
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"Source rectangle: {sourceRect} (from {sourceRect.X},{sourceRect.Y} to {sourceRect.Right},{sourceRect.Bottom})");
                            
                            // Define the destination rectangle (full size of the new bitmap)
                            System.Drawing.Rectangle destRect = new System.Drawing.Rectangle(0, 0, finalWidth, finalHeight);
                            System.Diagnostics.Debug.WriteLine($"Destination rectangle: {destRect}");
                            
                            // Copy the cropped region
                            cropGraphics.DrawImage(fullBitmap, destRect, sourceRect, System.Drawing.GraphicsUnit.Pixel);
                            System.Diagnostics.Debug.WriteLine("Image cropping completed");
                        }
                        

                        
                        // Process the image to remove specific color and make it transparent
                        int pixelsProcessed = RemoveColorAndMakeTransparent(processedBitmap, System.Drawing.Color.FromArgb(51, 102, 153), 3);
                        System.Diagnostics.Debug.WriteLine($"Color processing completed, {pixelsProcessed} pixels made transparent");
                    
                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                            System.Diagnostics.Debug.WriteLine($"Created directory: {directory}");
                    }
                    
                        // Save the processed screenshot with transparency
                        processedBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                        System.Diagnostics.Debug.WriteLine($"Saved final processed image: {filePath}");
                    
                    // Verify file was created and has content
                    if (File.Exists(filePath))
                    {
                        FileInfo fileInfo = new FileInfo(filePath);
                            System.Diagnostics.Debug.WriteLine($"File verification: exists={File.Exists(filePath)}, size={fileInfo.Length} bytes");
                            
                            // Restore rendering settings
                            RestoreOriginalRenderingSettings();
                            
                        return fileInfo.Length > 0;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("ERROR: Final file was not created");
                            
                            // Restore rendering settings
                            RestoreOriginalRenderingSettings();
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("=== TakeGDIScreenshot END (failed) ===");
                
                // Restore rendering settings
                RestoreOriginalRenderingSettings();
                
                return false;
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                System.Diagnostics.Debug.WriteLine($"=== TakeGDIScreenshot EXCEPTION ===");
                System.Diagnostics.Debug.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Ensure rendering settings are restored even on exception
                RestoreOriginalRenderingSettings();
                
                return false;
            }
        }

        /// <summary>
        /// Removes a specific color from the bitmap and makes it transparent
        /// </summary>
        /// <param name="bitmap">The bitmap to process</param>
        /// <param name="colorToRemove">The color to remove (RGB)</param>
        /// <param name="threshold">The tolerance threshold for color matching</param>
        /// <returns>Number of pixels that were made transparent</returns>
        private int RemoveColorAndMakeTransparent(System.Drawing.Bitmap bitmap, System.Drawing.Color colorToRemove, int threshold)
        {
            int pixelsProcessed = 0;
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"Color removal - Target: RGB({colorToRemove.R},{colorToRemove.G},{colorToRemove.B}), Threshold: {threshold}");
                
                // Lock the bitmap's bits for direct pixel manipulation
                System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadWrite,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* ptr = (byte*)bitmapData.Scan0.ToPointer();
                    int bytesPerPixel = 4; // ARGB = 4 bytes per pixel
                    
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            int index = (y * bitmapData.Stride) + (x * bytesPerPixel);
                            
                            byte blue = ptr[index];
                            byte green = ptr[index + 1];
                            byte red = ptr[index + 2];
                            byte alpha = ptr[index + 3];
                            
                            // Check if the current pixel color is close to the target color
                            if (IsColorSimilar(red, green, blue, colorToRemove.R, colorToRemove.G, colorToRemove.B, threshold))
                            {
                                // Make this pixel transparent
                                ptr[index + 3] = 0; // Set alpha to 0 (fully transparent)
                                pixelsProcessed++;
                            }
                        }
                    }
                }

                bitmap.UnlockBits(bitmapData);
                System.Diagnostics.Debug.WriteLine($"Color removal completed: {pixelsProcessed} pixels processed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Color removal exception: {ex.Message}");
            }
            
            return pixelsProcessed;
        }

        /// <summary>
        /// Checks if two colors are similar within a given threshold
        /// </summary>
        /// <param name="r1">Red component of first color</param>
        /// <param name="g1">Green component of first color</param>
        /// <param name="b1">Blue component of first color</param>
        /// <param name="r2">Red component of second color</param>
        /// <param name="g2">Green component of second color</param>
        /// <param name="b2">Blue component of second color</param>
        /// <param name="threshold">Maximum allowed difference per channel</param>
        /// <returns>True if colors are similar within the threshold</returns>
        private bool IsColorSimilar(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2, int threshold)
        {
            return Math.Abs(r1 - r2) <= threshold &&
                   Math.Abs(g1 - g2) <= threshold &&
                   Math.Abs(b1 - b2) <= threshold;
        }

        /// <summary>
        /// Finds the smallest bounding rectangle that contains all non-transparent pixels
        /// </summary>
        /// <param name="bitmap">The bitmap to analyze</param>
        /// <param name="padding">Padding to add around the bounding box</param>
        /// <returns>Rectangle representing the bounding box with padding</returns>
        private System.Drawing.Rectangle FindContentBounds(System.Drawing.Bitmap bitmap, int padding = 2)
        {
            int minX = bitmap.Width;
            int minY = bitmap.Height;
            int maxX = -1;
            int maxY = -1;

            try
            {
                System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* ptr = (byte*)bitmapData.Scan0.ToPointer();
                    int bytesPerPixel = 4;
                    
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            int index = (y * bitmapData.Stride) + (x * bytesPerPixel);
                            byte alpha = ptr[index + 3];
                            
                            // If pixel is not transparent (alpha > 0)
                            if (alpha > 0)
                            {
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                        }
                    }
                }

                bitmap.UnlockBits(bitmapData);

                // If no content found, return full image bounds
                if (maxX == -1 || maxY == -1)
                {
                    System.Diagnostics.Debug.WriteLine("No content found in image, using full bounds");
                    return new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
                }

                // Add padding and ensure bounds are within image
                minX = Math.Max(0, minX - padding);
                minY = Math.Max(0, minY - padding);
                maxX = Math.Min(bitmap.Width - 1, maxX + padding);
                maxY = Math.Min(bitmap.Height - 1, maxY + padding);

                int width = maxX - minX + 1;
                int height = maxY - minY + 1;

                // Make it SQUARE by using the larger dimension
                int squareSize = Math.Max(width, height);
                
                // Center the square within the content bounds
                int centerX = minX + width / 2;
                int centerY = minY + height / 2;
                
                int squareMinX = centerX - squareSize / 2;
                int squareMinY = centerY - squareSize / 2;
                
                // Ensure the square fits within the image bounds
                squareMinX = Math.Max(0, Math.Min(squareMinX, bitmap.Width - squareSize));
                squareMinY = Math.Max(0, Math.Min(squareMinY, bitmap.Height - squareSize));
                
                // If the square is too big for the image, resize it
                if (squareMinX + squareSize > bitmap.Width)
                {
                    squareSize = bitmap.Width - squareMinX;
                }
                if (squareMinY + squareSize > bitmap.Height)
                {
                    squareSize = bitmap.Height - squareMinY;
                }

                System.Diagnostics.Debug.WriteLine($"Content bounds found: ({minX},{minY}) to ({maxX},{maxY}), size: {width}x{height}");
                System.Diagnostics.Debug.WriteLine($"Square crop area: ({squareMinX},{squareMinY}), size: {squareSize}x{squareSize}");
                
                return new System.Drawing.Rectangle(squareMinX, squareMinY, squareSize, squareSize);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindContentBounds exception: {ex.Message}");
                return new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            }
        }

        /// <summary>
        /// Crops a bitmap to the specified rectangle
        /// </summary>
        /// <param name="source">Source bitmap to crop</param>
        /// <param name="cropArea">Rectangle defining the crop area</param>
        /// <returns>Cropped bitmap</returns>
        private System.Drawing.Bitmap CropBitmap(System.Drawing.Bitmap source, System.Drawing.Rectangle cropArea)
        {
            try
            {
                // Ensure crop area is within source bounds
                cropArea = System.Drawing.Rectangle.Intersect(cropArea, new System.Drawing.Rectangle(0, 0, source.Width, source.Height));
                
                if (cropArea.Width <= 0 || cropArea.Height <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("Invalid crop area, returning clone of original");
                    return (System.Drawing.Bitmap)source.Clone();
                }

                System.Drawing.Bitmap cropped = new System.Drawing.Bitmap(cropArea.Width, cropArea.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(cropped))
                {
                    graphics.DrawImage(source, new System.Drawing.Rectangle(0, 0, cropArea.Width, cropArea.Height), cropArea, System.Drawing.GraphicsUnit.Pixel);
                }
                
                System.Diagnostics.Debug.WriteLine($"Bitmap cropped from {source.Width}x{source.Height} to {cropped.Width}x{cropped.Height}");
                return cropped;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CropBitmap exception: {ex.Message}");
                return (System.Drawing.Bitmap)source.Clone();
            }
        }

        /// <summary>
        /// Removes blue pixels from the image only if they represent less than 30% of the total pixels
        /// This prevents removing actual blue clothing while cleaning up remaining background artifacts
        /// </summary>
        /// <param name="bitmap">The bitmap to process</param>
        /// <param name="blueColor">The blue color to remove</param>
        /// <param name="threshold">Color matching threshold</param>
        /// <returns>Number of pixels processed, or -1 if skipped due to >30% blue</returns>
        private int RemoveBluePixelsIfMinority(System.Drawing.Bitmap bitmap, System.Drawing.Color blueColor, int threshold)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Checking blue pixel percentage before removal...");
                
                int totalPixels = bitmap.Width * bitmap.Height;
                int bluePixelCount = 0;
                int pixelsProcessed = 0;
                
                System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadWrite,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* ptr = (byte*)bitmapData.Scan0.ToPointer();
                    int bytesPerPixel = 4;
                    
                    // First pass: count blue pixels
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            int index = (y * bitmapData.Stride) + (x * bytesPerPixel);
                            
                            byte blue = ptr[index];
                            byte green = ptr[index + 1];
                            byte red = ptr[index + 2];
                            byte alpha = ptr[index + 3];
                            
                            // Only count non-transparent pixels
                            if (alpha > 0 && IsColorSimilar(red, green, blue, blueColor.R, blueColor.G, blueColor.B, threshold))
                            {
                                bluePixelCount++;
                            }
                        }
                    }
                    
                    // Calculate percentage of blue pixels (only counting non-transparent pixels)
                    int nonTransparentPixels = 0;
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            int index = (y * bitmapData.Stride) + (x * bytesPerPixel);
                            byte alpha = ptr[index + 3];
                            if (alpha > 0) nonTransparentPixels++;
                        }
                    }
                    
                    double bluePercentage = nonTransparentPixels > 0 ? (double)bluePixelCount / nonTransparentPixels * 100.0 : 0.0;
                    System.Diagnostics.Debug.WriteLine($"Blue pixels: {bluePixelCount}/{nonTransparentPixels} ({bluePercentage:F1}%)");
                    
                    // Only remove blue pixels if they represent less than 30% of non-transparent pixels
                    if (bluePercentage >= 30.0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Blue pixels >= 30%, skipping removal (likely blue clothing)");
                        bitmap.UnlockBits(bitmapData);
                        return -1; // Return -1 to indicate skipped
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Blue pixels < 30%, proceeding with removal");
                    
                    // Second pass: remove blue pixels
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            int index = (y * bitmapData.Stride) + (x * bytesPerPixel);
                            
                            byte blue = ptr[index];
                            byte green = ptr[index + 1];
                            byte red = ptr[index + 2];
                            byte alpha = ptr[index + 3];
                            
                            // Remove blue pixels (make them transparent)
                            if (alpha > 0 && IsColorSimilar(red, green, blue, blueColor.R, blueColor.G, blueColor.B, threshold))
                            {
                                ptr[index + 3] = 0; // Set alpha to 0 (transparent)
                                pixelsProcessed++;
                            }
                        }
                    }
                }

                bitmap.UnlockBits(bitmapData);
                System.Diagnostics.Debug.WriteLine($"Blue pixel cleanup completed: {pixelsProcessed} pixels made transparent");
                return pixelsProcessed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoveBluePixelsIfMinority exception: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Parses resolution string (e.g., "1024x1024") into width and height
        /// </summary>
        /// <param name="resolution">Resolution string in format "widthxheight"</param>
        /// <returns>Tuple of (width, height), defaults to (1024, 1024) if parsing fails</returns>
        private (int width, int height) ParseResolution(string resolution)
        {
            try
            {
                if (string.IsNullOrEmpty(resolution))
                    return (1024, 1024);

                string[] parts = resolution.ToLower().Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
                {
                    return (width, height);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ParseResolution exception: {ex.Message}");
            }
            
            return (1024, 1024); // Default resolution
        }

        /// <summary>
        /// Stores the original window size for restoration
        /// </summary>
        private System.Drawing.Size originalWindowSize = System.Drawing.Size.Empty;

        /// <summary>
        /// Sets the window size to the specified render resolution
        /// </summary>
        /// <param name="renderResolution">Resolution string in format "widthxheight"</param>
        private void SetRenderResolution(string renderResolution)
        {
            try
            {
                if (originalWindowSize.IsEmpty)
                {
                    originalWindowSize = this.Size;
                    System.Diagnostics.Debug.WriteLine($"Stored original window size: {originalWindowSize.Width}x{originalWindowSize.Height}");
                }

                var (width, height) = ParseResolution(renderResolution);
                
                if (this.Size.Width != width || this.Size.Height != height)
                {
                    this.Size = new System.Drawing.Size(width, height);
                    System.Diagnostics.Debug.WriteLine($"Window resized to render resolution: {width}x{height}");
                    
                    // Give the renderer time to adjust to the new size
                    System.Threading.Thread.Sleep(100);
                    Application.DoEvents();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetRenderResolution exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores the window to its original size
        /// </summary>
        private void RestoreOriginalResolution()
        {
            try
            {
                if (!originalWindowSize.IsEmpty)
                {
                    this.Size = originalWindowSize;
                    System.Diagnostics.Debug.WriteLine($"Window restored to original size: {originalWindowSize.Width}x{originalWindowSize.Height}");
                    
                    // Give the renderer time to adjust to the restored size
                    System.Threading.Thread.Sleep(100);
                    Application.DoEvents();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreOriginalResolution exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Scales a bitmap to the specified output resolution
        /// </summary>
        /// <param name="source">Source bitmap to scale</param>
        /// <param name="outputResolution">Target resolution string in format "widthxheight"</param>
        /// <returns>Scaled bitmap</returns>
        private System.Drawing.Bitmap ScaleToOutputResolution(System.Drawing.Bitmap source, string outputResolution)
        {
            try
            {
                var (targetWidth, targetHeight) = ParseResolution(outputResolution);
                
                if (source.Width == targetWidth && source.Height == targetHeight)
                {
                    return (System.Drawing.Bitmap)source.Clone();
                }

                System.Drawing.Bitmap scaled = new System.Drawing.Bitmap(targetWidth, targetHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(scaled))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    
                    graphics.DrawImage(source, new System.Drawing.Rectangle(0, 0, targetWidth, targetHeight), 
                                     new System.Drawing.Rectangle(0, 0, source.Width, source.Height), 
                                     System.Drawing.GraphicsUnit.Pixel);
                }
                
                System.Diagnostics.Debug.WriteLine($"Image scaled from {source.Width}x{source.Height} to {targetWidth}x{targetHeight}");
                return scaled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScaleToOutputResolution exception: {ex.Message}");
                return (System.Drawing.Bitmap)source.Clone();
            }
        }

        /// <summary>
        /// Stores the original wireframe setting for restoration
        /// </summary>
        private bool originalWireframeSetting = false;
        private bool wireframeBackupTaken = false;

        /// <summary>
        /// Enables double-sided rendering for better screenshot quality
        /// This forces all geometry to use double-sided rasterizer states (CullMode.None)
        /// </summary>
        private void EnableDoubleSidedRendering()
        {
            try
            {
                if (Renderer?.shaders != null)
                {
                    // Store original wireframe setting
                    if (!wireframeBackupTaken)
                    {
                        originalWireframeSetting = Renderer.shaders.wireframe;
                        wireframeBackupTaken = true;
                        System.Diagnostics.Debug.WriteLine($"Stored original wireframe setting: {originalWireframeSetting}");
                    }

                    // Force wireframe to false to use solid double-sided rendering
                    // CodeWalker automatically uses rsSolidDblSided for cloth/cutout batches when wireframe is false
                    Renderer.shaders.wireframe = false;
                    System.Diagnostics.Debug.WriteLine("Double-sided rendering enabled for screenshots");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnableDoubleSidedRendering exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores the original rendering settings after screenshots
        /// </summary>
        private void RestoreOriginalRenderingSettings()
        {
            try
            {
                if (Renderer?.shaders != null && wireframeBackupTaken)
                {
                    Renderer.shaders.wireframe = originalWireframeSetting;
                    wireframeBackupTaken = false;
                    System.Diagnostics.Debug.WriteLine($"Restored original wireframe setting: {originalWireframeSetting}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreOriginalRenderingSettings exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Pre-generates alpha mask for a cloth and caches it for later use
        /// This should be called BEFORE processing any texture indices
        /// </summary>
        /// <param name="clothName">Name of the cloth to generate alpha mask for</param>
        /// <returns>True if alpha mask was generated and cached successfully</returns>
        public bool PreGenerateAlphaMask(string clothName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== PRE-GENERATE ALPHA MASK DEBUG ===");
                System.Diagnostics.Debug.WriteLine($"Input clothName: '{clothName}'");
                
                if (string.IsNullOrEmpty(clothName))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ PreGenerateAlphaMask: clothName is null or empty");
                    return false;
                }
                
                // Check if alpha mask is already cached
                if (alphaMaskCache.ContainsKey(clothName))
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Alpha mask for '{clothName}' already exists in cache");
                    var existingMask = alphaMaskCache[clothName];
                    System.Diagnostics.Debug.WriteLine($"Existing mask size: {existingMask?.Width}x{existingMask?.Height}");
                    return true;
                }
                
                System.Diagnostics.Debug.WriteLine($"🔄 Pre-generating alpha mask for cloth: {clothName}");
                System.Diagnostics.Debug.WriteLine($"Current cache count before generation: {alphaMaskCache.Count}");
                
                // Generate the alpha mask (this will cache it internally)
                System.Drawing.Bitmap alphaMask = GenerateAlphaMask(clothName);
                
                if (alphaMask != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Alpha mask generated successfully: {alphaMask.Width}x{alphaMask.Height}");
                    System.Diagnostics.Debug.WriteLine($"Cache count after generation: {alphaMaskCache.Count}");
                    System.Diagnostics.Debug.WriteLine($"Cache contains key '{clothName}': {alphaMaskCache.ContainsKey(clothName)}");
                    
                    alphaMask.Dispose(); // We only need it cached, not the returned reference
                    System.Diagnostics.Debug.WriteLine($"✓ Alpha mask pre-generated and cached for '{clothName}'");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Failed to pre-generate alpha mask for '{clothName}'");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ PreGenerateAlphaMask exception: {ex.Message}");
                return false;
            }
        }
    }
}

