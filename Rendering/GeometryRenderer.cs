using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sm64DecompLevelViewer.Models;
using Sm64DecompLevelViewer.Services;
using System.IO;
using System.Linq;

namespace Sm64DecompLevelViewer.Rendering;

public class GeometryRenderer : GameWindow
{
    private CollisionMesh? _collisionMesh;
    private VisualMesh? _visualMesh;
    private List<LevelObject>? _objects;
    private const float VISUAL_OBJECT_SIZE = 100f;
    private const float HITBOX_OBJECT_SIZE = 300f;
    private Matrix4 _view;
    private Matrix4 _projection;
    private Vector3 _cameraPosition;
    private Vector3 _cameraTarget;
    private float _cameraDistance = 5000f;
    private float _cameraYaw = 45f;
    private float _cameraPitch = 30f;
    private bool _isRightMouseDown = false;
    private Vector2 _lastMousePos;

    // Collision rendering (wireframe)
    private int _collisionShaderProgram;
    private int _collisionVao;
    private int _collisionVbo;
    private int _collisionVertexCount;

    // Visual rendering (solid with lighting)
    private int _visualShaderProgram;
    private int _visualVao;
    private int _visualVbo;
    private int _visualVertexCount;

    // Object rendering (colored cubes)
    private int _objectShaderProgram;
    private int _objectVao;
    private int _objectVbo;
    private int _objectVertexCount;

    // Object selection
    private LevelObject? _selectedObject;
    private int _selectedObjectIndex = -1;

    private bool _showCollision = true;
    private bool _showVisual = true;
    private bool _showObjects = true;
    private HashSet<int> _hiddenSubModels = new(); // Track which sub-models are hidden

    public event Action<LevelObject?>? ObjectSelected;

    public GeometryRenderer(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    {
        _cameraPosition = new Vector3(0, 1000, 3000);
        _cameraTarget = Vector3.Zero;
        
        // Update title with controls
        Title += " | Controls: [WASD] Move | [Right-Click] Rotate | [C] Collision | [V] Visual | [1-9] Hide Sub-Model | [0] Show All | [R] Reset";
    }

    public void LoadMesh(CollisionMesh mesh)
    {
        _collisionMesh = mesh;
        UploadCollisionMeshData();
    }

    public void LoadVisualMesh(VisualMesh mesh)
    {
        _visualMesh = mesh;
        UploadVisualMeshData();
    }

    public void SetObjects(List<LevelObject> objects)
    {
        _objects = objects;
        UploadObjectData();
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(0.1f, 0.1f, 0.15f, 1.0f);
        GL.Enable(EnableCap.DepthTest);

        _collisionShaderProgram = CreateCollisionShader();
        _visualShaderProgram = CreateVisualShader();
        _objectShaderProgram = CreateObjectShader();

        UpdateProjection();
        
        // Ensure window has focus for keyboard/mouse input
        Focus();
        Console.WriteLine("3D Viewer window loaded and focused");
    }

    private int CreateCollisionShader()
    {
        string vertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            uniform mat4 uMVP;
            void main()
            {
                gl_Position = uMVP * vec4(aPosition, 1.0);
            }
        ";

        string fragmentShaderSource = @"
            #version 330 core
            out vec4 FragColor;
            void main()
            {
                FragColor = vec4(0.0, 1.0, 0.0, 1.0); // Green wireframe
            }
        ";

        return CompileShaderProgram(vertexShaderSource, fragmentShaderSource);
    }

    private int CreateVisualShader()
    {
        string vertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec3 aNormal;
            
            uniform mat4 uMVP;
            
            out vec3 vNormal;
            out vec3 vPosition;
            
            void main()
            {
                gl_Position = uMVP * vec4(aPosition, 1.0);
                vNormal = aNormal;
                vPosition = aPosition;
            }
        ";

        string fragmentShaderSource = @"
            #version 330 core
            in vec3 vNormal;
            in vec3 vPosition;
            
            uniform vec3 uLightDir;
            
            out vec4 FragColor;
            
            void main()
            {
                vec3 norm = normalize(vNormal);
                vec3 lightDir = normalize(uLightDir);
                
                float diff = max(dot(norm, lightDir), 0.0);
                float ambient = 0.3;
                float lighting = ambient + diff * 0.7;
                
                vec3 baseColor = vec3(0.7, 0.7, 0.8);
                vec3 color = baseColor * lighting;
                FragColor = vec4(color, 1.0);
            }
        ";

        return CompileShaderProgram(vertexShaderSource, fragmentShaderSource);
    }

    private int CreateObjectShader()
    {
        string vertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec3 aColor;
            
            uniform mat4 uMVP;
            
            out vec3 vColor;
            
            void main()
            {
                gl_Position = uMVP * vec4(aPosition, 1.0);
                vColor = aColor;
            }
        ";

        string fragmentShaderSource = @"
            #version 330 core
            in vec3 vColor;
            
            out vec4 FragColor;
            
            void main()
            {
                FragColor = vec4(vColor, 1.0);
            }
        ";

        return CompileShaderProgram(vertexShaderSource, fragmentShaderSource);
    }

    private int CompileShaderProgram(string vertexSource, string fragmentSource)
    {
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);

        GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vertexSuccess);
        if (vertexSuccess == 0)
        {
            string infoLog = GL.GetShaderInfoLog(vertexShader);
            Console.WriteLine($"Vertex Shader Error: {infoLog}");
        }

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);

        GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fragmentSuccess);
        if (fragmentSuccess == 0)
        {
            string infoLog = GL.GetShaderInfoLog(fragmentShader);
            Console.WriteLine($"Fragment Shader Error: {infoLog}");
        }

        int shaderProgram = GL.CreateProgram();
        GL.AttachShader(shaderProgram, vertexShader);
        GL.AttachShader(shaderProgram, fragmentShader);
        GL.LinkProgram(shaderProgram);

        GL.GetProgram(shaderProgram, GetProgramParameterName.LinkStatus, out int linkSuccess);
        if (linkSuccess == 0)
        {
            string infoLog = GL.GetProgramInfoLog(shaderProgram);
            Console.WriteLine($"Shader Program Link Error: {infoLog}");
        }

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return shaderProgram;
    }

    private void UploadCollisionMeshData()
    {
        if (_collisionMesh == null || _collisionMesh.TriangleCount == 0)
            return;

        List<float> vertices = new List<float>();

        foreach (var tri in _collisionMesh.Triangles)
        {
            if (tri.V1 < _collisionMesh.Vertices.Count && tri.V2 < _collisionMesh.Vertices.Count && tri.V3 < _collisionMesh.Vertices.Count)
            {
                var v1 = _collisionMesh.Vertices[tri.V1];
                var v2 = _collisionMesh.Vertices[tri.V2];
                var v3 = _collisionMesh.Vertices[tri.V3];

                // Line 1: v1 -> v2
                vertices.Add(v1.X); vertices.Add(v1.Y); vertices.Add(v1.Z);
                vertices.Add(v2.X); vertices.Add(v2.Y); vertices.Add(v2.Z);

                // Line 2: v2 -> v3
                vertices.Add(v2.X); vertices.Add(v2.Y); vertices.Add(v2.Z);
                vertices.Add(v3.X); vertices.Add(v3.Y); vertices.Add(v3.Z);

                // Line 3: v3 -> v1
                vertices.Add(v3.X); vertices.Add(v3.Y); vertices.Add(v3.Z);
                vertices.Add(v1.X); vertices.Add(v1.Y); vertices.Add(v1.Z);
            }
        }

        _collisionVertexCount = vertices.Count / 3;

        _collisionVao = GL.GenVertexArray();
        _collisionVbo = GL.GenBuffer();

        GL.BindVertexArray(_collisionVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _collisionVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        GL.BindVertexArray(0);
    }

    private void UploadVisualMeshData()
    {
        try
        {
            if (_visualMesh == null || (_visualMesh.Triangles.Count == 0 && _visualMesh.SubMeshes.Count == 0))
            {
                _visualVertexCount = 0;
                return;
            }

            List<float> vertices = new List<float>();

            // Prioritize SubMeshes if available (for visibility control)
            // Otherwise fall back to merged mesh
            if (_visualMesh.SubMeshes.Count > 0)
            {
                // Render each sub-mesh separately with visibility control
                int skippedCount = 0;
                int renderedCount = 0;
                
                foreach (var subMesh in _visualMesh.SubMeshes)
                {
                    // Skip hidden sub-models
                    if (!subMesh.IsVisible)
                    {
                        skippedCount++;
                        continue;
                    }
                    
                    renderedCount++;

                    foreach (var tri in subMesh.Triangles)
                    {
                        if (tri.V1 < subMesh.Vertices.Count && tri.V2 < subMesh.Vertices.Count && tri.V3 < subMesh.Vertices.Count)
                        {
                            var v1 = subMesh.Vertices[tri.V1];
                            var v2 = subMesh.Vertices[tri.V2];
                            var v3 = subMesh.Vertices[tri.V3];

                            // Vertex 1: position + normal
                            vertices.Add(v1.X); vertices.Add(v1.Y); vertices.Add(v1.Z);
                            var n1 = v1.GetNormalizedNormal();
                            vertices.Add(n1.nx); vertices.Add(n1.ny); vertices.Add(n1.nz);

                            // Vertex 2: position + normal
                            vertices.Add(v2.X); vertices.Add(v2.Y); vertices.Add(v2.Z);
                            var n2 = v2.GetNormalizedNormal();
                            vertices.Add(n2.nx); vertices.Add(n2.ny); vertices.Add(n2.nz);

                            // Vertex 3: position + normal
                            vertices.Add(v3.X); vertices.Add(v3.Y); vertices.Add(v3.Z);
                            var n3 = v3.GetNormalizedNormal();
                            vertices.Add(n3.nx); vertices.Add(n3.ny); vertices.Add(n3.nz);
                        }
                    }
                }
            }
            else if (_visualMesh.Triangles.Count > 0)
            {
                // Fall back to merged mesh if no sub-meshes
                foreach (var tri in _visualMesh.Triangles)
                {
                    if (tri.V1 < _visualMesh.Vertices.Count && tri.V2 < _visualMesh.Vertices.Count && tri.V3 < _visualMesh.Vertices.Count)
                    {
                        var v1 = _visualMesh.Vertices[tri.V1];
                        var v2 = _visualMesh.Vertices[tri.V2];
                        var v3 = _visualMesh.Vertices[tri.V3];

                        // Vertex 1: position + normal
                        vertices.Add(v1.X); vertices.Add(v1.Y); vertices.Add(v1.Z);
                        var n1 = v1.GetNormalizedNormal();
                        vertices.Add(n1.nx); vertices.Add(n1.ny); vertices.Add(n1.nz);

                        // Vertex 2: position + normal
                        vertices.Add(v2.X); vertices.Add(v2.Y); vertices.Add(v2.Z);
                        var n2 = v2.GetNormalizedNormal();
                        vertices.Add(n2.nx); vertices.Add(n2.ny); vertices.Add(n2.nz);

                        // Vertex 3: position + normal
                        vertices.Add(v3.X); vertices.Add(v3.Y); vertices.Add(v3.Z);
                        var n3 = v3.GetNormalizedNormal();
                        vertices.Add(n3.nx); vertices.Add(n3.ny); vertices.Add(n3.nz);
                    }
                }
            }

            _visualVertexCount = vertices.Count / 6; // 6 floats per vertex (3 pos + 3 normal)

            if (_visualVertexCount == 0)
                return;

            _visualVao = GL.GenVertexArray();
            _visualVbo = GL.GenBuffer();

            GL.BindVertexArray(_visualVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _visualVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);

            // Position attribute (location = 0)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Normal attribute (location = 1)
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            Console.WriteLine($"Uploaded {_visualVertexCount} visual vertices ({_visualVertexCount / 3} triangles)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in UploadVisualMeshData: {ex.Message}");
            _visualVertexCount = 0;
        }
    }

    private void UploadObjectData()
    {
        if (_objects == null || _objects.Count == 0)
        {
            _objectVertexCount = 0;
            Console.WriteLine("No objects to upload");
            return;
        }

        try
        {
            List<float> vertices = new List<float>();
            float cubeSize = VISUAL_OBJECT_SIZE; // Controlled visually

            for (int i = 0; i < _objects.Count; i++)
            {
                var obj = _objects[i];
                
                // Create a small cube at object position
                float x = obj.X;
                float y = obj.Y;
                float z = obj.Z;
                float s = cubeSize / 2;

                // Check if this is the selected object
                bool isSelected = (i == _selectedObjectIndex);

                // Color based on selection or model type
                Vector3 color;
                if (isSelected)
                {
                    color = new Vector3(1.0f, 1.0f, 1.0f); // White for selected object
                }
                else
                {
                    color = obj.ModelName.Contains("MODEL_NONE") ? new Vector3(1.0f, 1.0f, 0.0f) : // Yellow for invisible objects
                           obj.ModelName.Contains("BUTTERFLY") ? new Vector3(1.0f, 0.5f, 1.0f) :  // Pink for butterflies
                           obj.ModelName.Contains("BOBOMB") ? new Vector3(1.0f, 0.0f, 0.0f) :     // Red for bob-ombs
                           new Vector3(0.0f, 1.0f, 1.0f); // Cyan for others
                }

                // Define 8 vertices of cube
                Vector3[] cubeVerts = new Vector3[]
                {
                    new Vector3(x-s, y-s, z-s), new Vector3(x+s, y-s, z-s),
                    new Vector3(x+s, y+s, z-s), new Vector3(x-s, y+s, z-s),
                    new Vector3(x-s, y-s, z+s), new Vector3(x+s, y-s, z+s),
                    new Vector3(x+s, y+s, z+s), new Vector3(x-s, y+s, z+s)
                };

                // Define 12 triangles (2 per face, 6 faces)
                int[][] faces = new int[][]
                {
                    new int[] {0,1,2}, new int[] {0,2,3}, // Front
                    new int[] {5,4,7}, new int[] {5,7,6}, // Back
                    new int[] {4,0,3}, new int[] {4,3,7}, // Left
                    new int[] {1,5,6}, new int[] {1,6,2}, // Right
                    new int[] {3,2,6}, new int[] {3,6,7}, // Top
                    new int[] {4,5,1}, new int[] {4,1,0}  // Bottom
                };

                foreach (var face in faces)
                {
                    foreach (var idx in face)
                    {
                        vertices.Add(cubeVerts[idx].X);
                        vertices.Add(cubeVerts[idx].Y);
                        vertices.Add(cubeVerts[idx].Z);
                        vertices.Add(color.X);
                        vertices.Add(color.Y);
                        vertices.Add(color.Z);
                    }
                }
            }

            _objectVertexCount = vertices.Count / 6; // 6 floats per vertex (3 pos + 3 color)

            if (_objectVertexCount == 0)
                return;

            _objectVao = GL.GenVertexArray();
            _objectVbo = GL.GenBuffer();

            GL.BindVertexArray(_objectVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _objectVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);

            // Position attribute (location = 0)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Color attribute (location = 1)
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            Console.WriteLine($"Uploaded {_objects.Count} objects as {_objectVertexCount / 3} triangles");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading object data: {ex.Message}");
            _objectVertexCount = 0;
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, Size.X, Size.Y);
        UpdateProjection();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        UpdateCamera();

        Matrix4 mvp = _view * _projection;

        // Render visual mesh (solid)
        if (_showVisual && _visualMesh != null && _visualVertexCount > 0)
        {
            GL.UseProgram(_visualShaderProgram);
            
            int mvpLocation = GL.GetUniformLocation(_visualShaderProgram, "uMVP");
            GL.UniformMatrix4(mvpLocation, false, ref mvp);
            
            int lightDirLocation = GL.GetUniformLocation(_visualShaderProgram, "uLightDir");
            GL.Uniform3(lightDirLocation, new Vector3(0.5f, 1.0f, 0.3f));

            GL.BindVertexArray(_visualVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _visualVertexCount);
            GL.BindVertexArray(0);
        }

        // Render collision mesh (wireframe) on top
        if (_showCollision && _collisionMesh != null && _collisionVertexCount > 0)
        {
            GL.UseProgram(_collisionShaderProgram);
            
            int mvpLocation = GL.GetUniformLocation(_collisionShaderProgram, "uMVP");
            GL.UniformMatrix4(mvpLocation, false, ref mvp);

            GL.BindVertexArray(_collisionVao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _collisionVertexCount);
            GL.BindVertexArray(0);
        }

        // Render objects (colored cubes)
        if (_showObjects && _objects != null && _objectVertexCount > 0)
        {
            GL.UseProgram(_objectShaderProgram);
            
            int mvpLocation = GL.GetUniformLocation(_objectShaderProgram, "uMVP");
            GL.UniformMatrix4(mvpLocation, false, ref mvp);

            GL.BindVertexArray(_objectVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _objectVertexCount);
            GL.BindVertexArray(0);
        }

        SwapBuffers();
    }

    private void UpdateCamera()
    {
        float radYaw = MathHelper.DegreesToRadians(_cameraYaw);
        float radPitch = MathHelper.DegreesToRadians(_cameraPitch);

        float x = _cameraDistance * MathF.Cos(radPitch) * MathF.Cos(radYaw);
        float y = _cameraDistance * MathF.Sin(radPitch);
        float z = _cameraDistance * MathF.Cos(radPitch) * MathF.Sin(radYaw);

        _cameraPosition = _cameraTarget + new Vector3(x, y, z);
        _view = Matrix4.LookAt(_cameraPosition, _cameraTarget, Vector3.UnitY);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        Console.WriteLine($"Mouse button pressed: {e.Button}");

        if (e.Button == MouseButton.Left && _objects != null && _objects.Count > 0)
        {
            Console.WriteLine($"Left click detected, checking {_objects.Count} objects");
            
            var mouseState = MouseState;
            Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);
            Console.WriteLine($"Mouse position: {mousePos}");
            
            int clickedObjectIndex = GetObjectAtScreenPosition(mousePos);
            Console.WriteLine($"Clicked object index: {clickedObjectIndex}");

            if (clickedObjectIndex >= 0)
            {
                _selectedObjectIndex = clickedObjectIndex;
                _selectedObject = _objects[clickedObjectIndex];
                Console.WriteLine($"Selected object: {_selectedObject.ModelName} at ({_selectedObject.X}, {_selectedObject.Y}, {_selectedObject.Z})");
                
                ObjectSelected?.Invoke(_selectedObject);
                
                UploadObjectData();
            }
            else
            {
                _selectedObjectIndex = -1;
                _selectedObject = null;
                Console.WriteLine("No object selected");
                
                ObjectSelected?.Invoke(null);
                
                UploadObjectData();
            }
        }
        else if (e.Button == MouseButton.Left)
        {
            Console.WriteLine($"Left click but objects is null or empty: objects={_objects?.Count ?? 0}");
        }
    }

    private int GetObjectAtScreenPosition(Vector2 screenPos)
    {
        // Convert screen coordinates to normalized device coordinates
        float x = (2.0f * screenPos.X) / Size.X - 1.0f;
        float y = 1.0f - (2.0f * screenPos.Y) / Size.Y;

        Vector4 nearPointNDC = new Vector4(x, y, -1.0f, 1.0f);
        Vector4 farPointNDC = new Vector4(x, y, 1.0f, 1.0f);

        Matrix4 invViewProj = Matrix4.Invert(_view * _projection);

        Vector4 nearPointWorld = nearPointNDC * invViewProj;
        Vector4 farPointWorld = farPointNDC * invViewProj;

        Vector3 nearPoint = nearPointWorld.Xyz / nearPointWorld.W;
        Vector3 farPoint = farPointWorld.Xyz / farPointWorld.W;

        Vector3 rayOrigin = nearPoint;
        Vector3 rayDir = Vector3.Normalize(farPoint - nearPoint);

        float closestDistance = float.MaxValue;
        int closestObjectIndex = -1;
        float cubeSize = HITBOX_OBJECT_SIZE; // Controlled for selection accuracy

        for (int i = 0; i < _objects!.Count; i++)
        {
            var obj = _objects[i];
            Vector3 objCenter = new Vector3(obj.X, obj.Y, obj.Z);
            float halfSize = cubeSize / 2;

            if (RayIntersectsBox(rayOrigin, rayDir, objCenter, halfSize, out float distance))
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestObjectIndex = i;
                }
            }
        }

        return closestObjectIndex;
    }

    private bool RayIntersectsBox(Vector3 rayOrigin, Vector3 rayDir, Vector3 boxCenter, float halfSize, out float distance)
    {
        distance = float.MaxValue;

        Vector3 boxMin = boxCenter - new Vector3(halfSize);
        Vector3 boxMax = boxCenter + new Vector3(halfSize);

        float tMin = 0.0f;
        float tMax = float.MaxValue;

        for (int i = 0; i < 3; i++)
        {
            float origin = i == 0 ? rayOrigin.X : (i == 1 ? rayOrigin.Y : rayOrigin.Z);
            float dir = i == 0 ? rayDir.X : (i == 1 ? rayDir.Y : rayDir.Z);
            float min = i == 0 ? boxMin.X : (i == 1 ? boxMin.Y : boxMin.Z);
            float max = i == 0 ? boxMax.X : (i == 1 ? boxMax.Y : boxMax.Z);

            if (MathF.Abs(dir) < 0.0001f)
            {
                if (origin < min || origin > max)
                    return false;
            }
            else
            {
                float t1 = (min - origin) / dir;
                float t2 = (max - origin) / dir;

                if (t1 > t2)
                {
                    float temp = t1;
                    t1 = t2;
                    t2 = temp;
                }

                tMin = MathF.Max(tMin, t1);
                tMax = MathF.Min(tMax, t2);

                if (tMin > tMax)
                    return false;
            }
        }

        distance = tMin;
        return true;
    }

    private void UpdateProjection()
    {
        _projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(45f),
            Size.X / (float)Size.Y,
            10f,
            100000f
        );
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        float moveSpeed = 4000f; // Units per second (increased from 100)
        float deltaTime = (float)args.Time;

        // WASD camera movement
        Vector3 forward = Vector3.Normalize(_cameraTarget - _cameraPosition);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));

        if (KeyboardState.IsKeyDown(Keys.W))
        {
            _cameraTarget += forward * moveSpeed * deltaTime;
        }
        if (KeyboardState.IsKeyDown(Keys.S))
        {
            _cameraTarget -= forward * moveSpeed * deltaTime;
        }
        if (KeyboardState.IsKeyDown(Keys.A))
        {
            _cameraTarget -= right * moveSpeed * deltaTime;
        }
        if (KeyboardState.IsKeyDown(Keys.D))
        {
            _cameraTarget += right * moveSpeed * deltaTime;
        }

        // Handle camera rotation with RIGHT mouse button
        if (MouseState.IsButtonDown(MouseButton.Right))
        {
            _cameraYaw += MouseState.Delta.X * 0.2f;
            _cameraPitch -= MouseState.Delta.Y * 0.2f;
            _cameraPitch = Math.Clamp(_cameraPitch, -89f, 89f);
        }

        // Handle zoom with mouse wheel
        _cameraDistance -= MouseState.ScrollDelta.Y * 200f;
        _cameraDistance = Math.Clamp(_cameraDistance, 100f, 50000f);

        // Toggle controls
        if (KeyboardState.IsKeyPressed(Keys.C))
        {
            _showCollision = !_showCollision;
            Console.WriteLine($"Collision mesh: {(_showCollision ? "ON" : "OFF")}");
        }

        if (KeyboardState.IsKeyPressed(Keys.V))
        {
            _showVisual = !_showVisual;
            Console.WriteLine($"Visual mesh: {(_showVisual ? "ON" : "OFF")}");
        }

        // Sub-model visibility controls (1-9 to toggle hide, 0 to show all)
        if (KeyboardState.IsKeyPressed(Keys.D0) || KeyboardState.IsKeyPressed(Keys.KeyPad0))
        {
            _hiddenSubModels.Clear();
            if (_visualMesh != null)
            {
                foreach (var subMesh in _visualMesh.SubMeshes)
                    subMesh.IsVisible = true;
                UploadVisualMeshData(); // Re-upload with all visible
            }
        }

        // Check each number key explicitly
        var numberKeys = new[] {
            (Keys.D1, Keys.KeyPad1, 1),
            (Keys.D2, Keys.KeyPad2, 2),
            (Keys.D3, Keys.KeyPad3, 3),
            (Keys.D4, Keys.KeyPad4, 4),
            (Keys.D5, Keys.KeyPad5, 5),
            (Keys.D6, Keys.KeyPad6, 6),
            (Keys.D7, Keys.KeyPad7, 7),
            (Keys.D8, Keys.KeyPad8, 8),
            (Keys.D9, Keys.KeyPad9, 9)
        };

        foreach (var (mainKey, keypadKey, i) in numberKeys)
        {
            if (KeyboardState.IsKeyPressed(mainKey) || KeyboardState.IsKeyPressed(keypadKey))
            {
                if (_hiddenSubModels.Contains(i))
                {
                    _hiddenSubModels.Remove(i);
                }
                else
                {
                    _hiddenSubModels.Add(i);
                }
                
                if (_visualMesh != null)
                {
                    var subMesh = _visualMesh.SubMeshes.FirstOrDefault(sm => sm.SubModelNumber == i);
                    if (subMesh != null)
                    {
                        subMesh.IsVisible = !_hiddenSubModels.Contains(i);
                        UploadVisualMeshData(); // Re-upload with updated visibility
                    }
                }
            }
        }

        // Reset camera
        if (KeyboardState.IsKeyPressed(Keys.R))
        {
            ResetCamera();
            Console.WriteLine("Camera reset");
        }
    }

    public void ResetCamera()
    {
        _cameraDistance = 5000f;
        _cameraYaw = 45f;
        _cameraPitch = 30f;
    }

    protected override void OnUnload()
    {
        base.OnUnload();

        GL.DeleteProgram(_collisionShaderProgram);
        GL.DeleteProgram(_visualShaderProgram);
        GL.DeleteVertexArray(_collisionVao);
        GL.DeleteBuffer(_collisionVbo);
        GL.DeleteVertexArray(_visualVao);
        GL.DeleteBuffer(_visualVbo);
    }
}
