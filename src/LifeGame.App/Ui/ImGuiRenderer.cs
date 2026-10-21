using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NumVector2 = System.Numerics.Vector2;

namespace LifeGame.App.Ui;

/// <summary>
/// ImGui を MonoGame で動かすための橋渡し。入力を ImGui へ渡し、ImGui が作った三角形の列を描く。
/// UIの組み立て(ボタンを押したときの処理を含む)は Update で行い、描画だけを Draw で行う。
/// </summary>
internal sealed unsafe class ImGuiRenderer : IDisposable
{
    private static readonly VertexDeclaration DrawVertexDeclaration = new(
        Unsafe.SizeOf<ImDrawVert>(),
        new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
        new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0));

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly RasterizerState _rasterizerState;
    private readonly Dictionary<IntPtr, Texture2D> _textures = [];
    private readonly (Keys Key, ImGuiKey ImGuiKey)[] _keyMap;
    private VertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;
    private byte[] _vertexData = [];
    private byte[] _indexData = [];
    private int _nextTextureId = 1;
    private int _scrollWheelValue;
    private int _horizontalScrollWheelValue;
    private bool _frameBegun;

    public ImGuiRenderer(Game game)
    {
        ImGui.SetCurrentContext(ImGui.CreateContext());
        var io = ImGui.GetIO();
        // ウィンドウの配置をファイル(imgui.ini)に残さない。配置はコードで決める
        io.NativePtr->IniFilename = null;

        _device = game.GraphicsDevice;
        _effect = new BasicEffect(_device) { TextureEnabled = true, VertexColorEnabled = true };
        _rasterizerState = new RasterizerState
        {
            CullMode = CullMode.None,
            FillMode = FillMode.Solid,
            ScissorTestEnable = true,
        };
        _keyMap = BuildKeyMap();

        var mouse = Mouse.GetState();
        _scrollWheelValue = mouse.ScrollWheelValue;
        _horizontalScrollWheelValue = mouse.HorizontalScrollWheelValue;
        game.Window.TextInput += (_, e) =>
        {
            if (!char.IsControl(e.Character))
                ImGui.GetIO().AddInputCharacter(e.Character);
        };
    }

    /// <summary>フォントを追加し終えたあとに呼び、文字の画像をGPUへ送る。</summary>
    public void BuildFontAtlas()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);
        var data = new byte[width * height * bytesPerPixel];
        Marshal.Copy((IntPtr)pixels, data, 0, data.Length);

        var texture = new Texture2D(_device, width, height, false, SurfaceFormat.Color);
        texture.SetData(data);
        io.Fonts.SetTexID(BindTexture(texture));
        io.Fonts.ClearTexData();
    }

    public IntPtr BindTexture(Texture2D texture)
    {
        var id = new IntPtr(_nextTextureId++);
        _textures[id] = texture;
        return id;
    }

    /// <summary>Update の最初に呼ぶ。入力を渡して、新しいUIのフレームを始める。</summary>
    public void BeginFrame(GameTime gameTime)
    {
        // Draw が飛ばされた場合でも、前のフレームを閉じてから始める
        if (_frameBegun)
            ImGui.EndFrame();

        var io = ImGui.GetIO();
        io.DisplaySize = new NumVector2(_device.PresentationParameters.BackBufferWidth, _device.PresentationParameters.BackBufferHeight);
        io.DisplayFramebufferScale = NumVector2.One;
        io.DeltaTime = Math.Max((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 1000f);
        UpdateInput(io);

        ImGui.NewFrame();
        _frameBegun = true;
    }

    /// <summary>Draw の最後に呼ぶ。組み立てたUIを描く。</summary>
    public void Render()
    {
        if (!_frameBegun)
            return;
        ImGui.Render();
        _frameBegun = false;
        RenderDrawData(ImGui.GetDrawData());
    }

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
            texture.Dispose();
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _effect.Dispose();
        _rasterizerState.Dispose();
    }

    private void UpdateInput(ImGuiIOPtr io)
    {
        var keyboard = Keyboard.GetState();
        foreach (var (key, imGuiKey) in _keyMap)
            io.AddKeyEvent(imGuiKey, keyboard.IsKeyDown(key));
        io.AddKeyEvent(ImGuiKey.ModCtrl, keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));
        io.AddKeyEvent(ImGuiKey.ModShift, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
        io.AddKeyEvent(ImGuiKey.ModAlt, keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));
        io.AddKeyEvent(ImGuiKey.ModSuper, keyboard.IsKeyDown(Keys.LeftWindows) || keyboard.IsKeyDown(Keys.RightWindows));

        var mouse = Mouse.GetState();
        io.AddMousePosEvent(mouse.X, mouse.Y);
        io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);

        int wheel = mouse.ScrollWheelValue - _scrollWheelValue;
        int horizontal = mouse.HorizontalScrollWheelValue - _horizontalScrollWheelValue;
        _scrollWheelValue = mouse.ScrollWheelValue;
        _horizontalScrollWheelValue = mouse.HorizontalScrollWheelValue;
        if (wheel != 0 || horizontal != 0)
            io.AddMouseWheelEvent(horizontal / 120f, wheel / 120f);
    }

    private void RenderDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0 || drawData.TotalVtxCount == 0)
            return;

        var lastViewport = _device.Viewport;
        var lastScissor = _device.ScissorRectangle;
        var lastRasterizer = _device.RasterizerState;
        var lastBlend = _device.BlendState;
        var lastDepth = _device.DepthStencilState;
        var lastSampler = _device.SamplerStates[0];

        _device.BlendFactor = Color.White;
        _device.BlendState = BlendState.NonPremultiplied;
        _device.RasterizerState = _rasterizerState;
        _device.DepthStencilState = DepthStencilState.None;
        _device.SamplerStates[0] = SamplerState.LinearClamp;
        _device.Viewport = new Viewport(0, 0, _device.PresentationParameters.BackBufferWidth, _device.PresentationParameters.BackBufferHeight);

        UpdateBuffers(drawData);
        RenderCommandLists(drawData);

        _device.Viewport = lastViewport;
        _device.ScissorRectangle = lastScissor;
        _device.RasterizerState = lastRasterizer;
        _device.BlendState = lastBlend;
        _device.DepthStencilState = lastDepth;
        _device.SamplerStates[0] = lastSampler;
    }

    private void UpdateBuffers(ImDrawDataPtr drawData)
    {
        int vertexSize = DrawVertexDeclaration.VertexStride;
        if (_vertexBuffer is null || _vertexBuffer.VertexCount < drawData.TotalVtxCount)
        {
            _vertexBuffer?.Dispose();
            int capacity = (int)(drawData.TotalVtxCount * 1.5f);
            _vertexBuffer = new VertexBuffer(_device, DrawVertexDeclaration, capacity, BufferUsage.None);
            _vertexData = new byte[capacity * vertexSize];
        }
        if (_indexBuffer is null || _indexBuffer.IndexCount < drawData.TotalIdxCount)
        {
            _indexBuffer?.Dispose();
            int capacity = (int)(drawData.TotalIdxCount * 1.5f);
            _indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, capacity, BufferUsage.None);
            _indexData = new byte[capacity * sizeof(ushort)];
        }

        int vertexOffset = 0;
        int indexOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var commandList = drawData.CmdLists[n];
            int vertexBytes = commandList.VtxBuffer.Size * vertexSize;
            int indexBytes = commandList.IdxBuffer.Size * sizeof(ushort);
            fixed (byte* vertexDestination = &_vertexData[vertexOffset * vertexSize])
            fixed (byte* indexDestination = &_indexData[indexOffset * sizeof(ushort)])
            {
                Buffer.MemoryCopy((void*)commandList.VtxBuffer.Data, vertexDestination, vertexBytes, vertexBytes);
                Buffer.MemoryCopy((void*)commandList.IdxBuffer.Data, indexDestination, indexBytes, indexBytes);
            }
            vertexOffset += commandList.VtxBuffer.Size;
            indexOffset += commandList.IdxBuffer.Size;
        }

        _vertexBuffer.SetData(_vertexData, 0, drawData.TotalVtxCount * vertexSize);
        _indexBuffer.SetData(_indexData, 0, drawData.TotalIdxCount * sizeof(ushort));
    }

    private void RenderCommandLists(ImDrawDataPtr drawData)
    {
        var io = ImGui.GetIO();
        _effect.World = Matrix.Identity;
        _effect.View = Matrix.Identity;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, io.DisplaySize.X, io.DisplaySize.Y, 0f, -1f, 1f);
        _device.SetVertexBuffer(_vertexBuffer);
        _device.Indices = _indexBuffer;

        int vertexOffset = 0;
        int indexOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var commandList = drawData.CmdLists[n];
            for (int i = 0; i < commandList.CmdBuffer.Size; i++)
            {
                var command = commandList.CmdBuffer[i];
                if (command.ElemCount == 0)
                    continue;
                if (!_textures.TryGetValue(command.TextureId, out var texture))
                    throw new InvalidOperationException($"ImGui が未登録のテクスチャ {command.TextureId} を使おうとしました。");

                var clip = command.ClipRect;
                _device.ScissorRectangle = new Rectangle((int)clip.X, (int)clip.Y, (int)(clip.Z - clip.X), (int)(clip.W - clip.Y));
                _effect.Texture = texture;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                        (int)command.VtxOffset + vertexOffset, (int)command.IdxOffset + indexOffset, (int)command.ElemCount / 3);
                }
            }
            vertexOffset += commandList.VtxBuffer.Size;
            indexOffset += commandList.IdxBuffer.Size;
        }
    }

    private static (Keys, ImGuiKey)[] BuildKeyMap()
    {
        var map = new List<(Keys, ImGuiKey)>
        {
            (Keys.Tab, ImGuiKey.Tab), (Keys.Left, ImGuiKey.LeftArrow), (Keys.Right, ImGuiKey.RightArrow),
            (Keys.Up, ImGuiKey.UpArrow), (Keys.Down, ImGuiKey.DownArrow), (Keys.PageUp, ImGuiKey.PageUp),
            (Keys.PageDown, ImGuiKey.PageDown), (Keys.Home, ImGuiKey.Home), (Keys.End, ImGuiKey.End),
            (Keys.Insert, ImGuiKey.Insert), (Keys.Delete, ImGuiKey.Delete), (Keys.Back, ImGuiKey.Backspace),
            (Keys.Space, ImGuiKey.Space), (Keys.Enter, ImGuiKey.Enter), (Keys.Escape, ImGuiKey.Escape),
            (Keys.LeftControl, ImGuiKey.LeftCtrl), (Keys.RightControl, ImGuiKey.RightCtrl),
            (Keys.LeftShift, ImGuiKey.LeftShift), (Keys.RightShift, ImGuiKey.RightShift),
            (Keys.LeftAlt, ImGuiKey.LeftAlt), (Keys.RightAlt, ImGuiKey.RightAlt),
        };
        for (int i = 0; i < 26; i++)
            map.Add((Keys.A + i, ImGuiKey.A + i));
        for (int i = 0; i < 10; i++)
        {
            map.Add((Keys.D0 + i, ImGuiKey._0 + i));
            map.Add((Keys.NumPad0 + i, ImGuiKey.Keypad0 + i));
        }
        for (int i = 0; i < 12; i++)
            map.Add((Keys.F1 + i, ImGuiKey.F1 + i));
        return map.ToArray();
    }
}
