using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

// Use aliases for System.Numerics types to resolve the CS0104 ambiguity with OpenTK.Mathematics
using SysVector2 = System.Numerics.Vector2;
using SysVector4 = System.Numerics.Vector4;

// Ensure this project targets a framework compatible with OpenTK (e.g., .NET 6+)

public static class Config
{
    public const int ScreenWidth = 800;
    public const int ScreenHeight = 600;
    public const string Title = "2D Collision Detection Prototype (OpenTK)";
}

// --- SHADER UTILITY ---
public class Shader
{
    public int Handle { get; private set; }

    // Shader Source Constants are now inside the class to prevent CS8803 (Top-level statement errors)
    private const string VertexShaderSource = @"
#version 330 core
layout (location = 0) in vec2 aPosition;

uniform mat4 projection;
uniform mat4 model;

void main()
{
    gl_Position = projection * model * vec4(aPosition, 0.0, 1.0);
}";

    private const string FragmentShaderSource = @"
#version 330 core
out vec4 FragColor;

uniform vec4 objectColor;

void main()
{
    FragColor = objectColor;
}";

    public Shader()
    {
        // 1. Compile Vertex Shader
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, VertexShaderSource);
        GL.CompileShader(vertexShader);
        CheckCompileErrors(vertexShader, "VERTEX");

        // 2. Compile Fragment Shader
        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, FragmentShaderSource);
        GL.CompileShader(fragmentShader);
        CheckCompileErrors(fragmentShader, "FRAGMENT");

        // 3. Link Program
        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vertexShader);
        GL.AttachShader(Handle, fragmentShader);
        GL.LinkProgram(Handle);
        CheckCompileErrors(Handle, "PROGRAM");

        // 4. Delete shaders as they are linked into the program
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    public void Use() => GL.UseProgram(Handle);

    public void SetMatrix4(string name, Matrix4 matrix)
    {
        int location = GL.GetUniformLocation(Handle, name);
        GL.UniformMatrix4(location, false, ref matrix);
    }

    public void SetVector4(string name, Vector4 vector)
    {
        int location = GL.GetUniformLocation(Handle, name);
        GL.Uniform4(location, vector);
    }

    private void CheckCompileErrors(int shader, string type)
    {
        if (type != "PROGRAM")
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                System.Console.WriteLine($"ERROR::SHADER_COMPILATION_ERROR of type: {type}\n{infoLog}");
            }
        }
        else
        {
            GL.GetProgram(shader, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(shader);
                System.Console.WriteLine($"ERROR::PROGRAM_LINKING_ERROR of type: {type}\n{infoLog}");
            }
        }
    }
}

// --- RENDERER CLASS ---
public class Renderer
{
    private Shader shader;
    private int quadVAO;
    private int quadVBO;

    // Defines a quad centered at (0,0) of size 1x1, useful for scaling
    private readonly float[] vertices = {
        // pos 
        0.0f, 1.0f,
        1.0f, 0.0f,
        0.0f, 0.0f,

        0.0f, 1.0f,
        1.0f, 1.0f,
        1.0f, 0.0f
    };

    public Renderer()
    {
        shader = new Shader(); // Shader constructor now handles loading source strings
        SetupMesh();

        // Orthographic projection matrix for 2D rendering
        Matrix4 projection = Matrix4.CreateOrthographicOffCenter(
            0, Config.ScreenWidth, Config.ScreenHeight, 0, -1.0f, 1.0f
        );
        shader.Use();
        shader.SetMatrix4("projection", projection);
    }

    private void SetupMesh()
    {
        quadVAO = GL.GenVertexArray();
        quadVBO = GL.GenBuffer();

        // FIX: Changed quadVAA to quadVAO
        GL.BindVertexArray(quadVAO);

        GL.BindBuffer(BufferTarget.ArrayBuffer, quadVBO);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        // Position attribute (layout location 0)
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
    }

    // This method uses OpenTK.Mathematics.Vector2 and Vector4 (no ambiguity)
    public void DrawQuad(Vector2 position, Vector2 size, Vector4 color)
    {
        shader.Use();

        // 1. Model Matrix for translation and scale
        Matrix4 model = Matrix4.Identity;
        // Translate
        model = Matrix4.CreateTranslation(position.X, position.Y, 0.0f) * model;
        // Scale
        model = Matrix4.CreateScale(size.X, size.Y, 1.0f) * model;

        shader.SetMatrix4("model", model);
        shader.SetVector4("objectColor", color);

        // 2. Draw the quad
        GL.BindVertexArray(quadVAO);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        GL.BindVertexArray(0);
    }
}

// --- GAME OBJECTS (Reusable Logic) ---

/// <summary>
/// Represents a rectangular object (Paddle, Brick, or Floor).
/// </summary>
public class GameObject
{
    // These use OpenTK.Mathematics types (Box2 and Vector4)
    public Box2 Rect;
    public Vector4 Color;
    public bool IsDestroyed;

    public GameObject(SysVector2 position, SysVector2 size, Vector4 color)
    {
        // Box2 uses OpenTK.Mathematics.Vector2
        Vector2 min = new Vector2(position.X, position.Y);
        Vector2 max = new Vector2(position.X + size.X, position.Y + size.Y);
        Rect = new Box2(min, max);
        Color = color;
        IsDestroyed = false;
    }

    public void Draw(Renderer renderer)
    {
        if (!IsDestroyed)
        {
            // renderer.DrawQuad expects OpenTK.Mathematics.Vector2
            renderer.DrawQuad(
                new Vector2(Rect.Min.X, Rect.Min.Y),
                new Vector2(Rect.Size.X, Rect.Size.Y),
                Color
            );
        }
    }
}

/// <summary>
/// Represents the moving circular ball (now rendered as a square).
/// </summary>
public class BallObject
{
    // These now explicitly use the aliased System.Numerics types
    public SysVector2 Position;
    public float Radius;
    public SysVector2 Velocity;
    public Vector4 Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // White

    public BallObject(SysVector2 position, float radius, SysVector2 velocity)
    {
        Position = position;
        Radius = radius;
        Velocity = velocity;
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
    }

    public void Draw(Renderer renderer)
    {
        // renderer.DrawQuad expects OpenTK.Mathematics.Vector2, so we convert SysVector2
        renderer.DrawQuad(
            new Vector2(Position.X - Radius, Position.Y - Radius),
            new Vector2(Radius * 2, Radius * 2),
            Color
        );
    }
}

// --- COLLISION LOGIC (Using C# Numerics and OpenTK.Mathematics) ---

public static class Collision
{
    // AABB - AABB Collision Check 
    public static bool CheckCollisionAABB(Box2 a, Box2 b)
    {
        // Check for overlap on X-axis:
        bool collisionX = a.Max.X >= b.Min.X && b.Max.X >= a.Min.X;

        // Check for overlap on Y-axis:
        bool collisionY = a.Max.Y >= b.Min.Y && b.Max.Y >= a.Min.Y;

        // Collision occurs only if both axes overlap
        return collisionX && collisionY;
    }

    // AABB - Circle Collision Check 
    public static bool CheckCollisionCircleAABB(BallObject ball, Box2 rect, out SysVector2 closestPoint)
    {
        // Convert ball position to OpenTK Vector2 for Math.Clamp
        Vector2 ballPosOpenTK = new Vector2(ball.Position.X, ball.Position.Y);

        // 1. Clamp the ball's center to the AABB boundaries to find the closest point
        Vector2 closestPointOpenTK;
        closestPointOpenTK.X = Math.Clamp(ballPosOpenTK.X, rect.Min.X, rect.Max.X);
        closestPointOpenTK.Y = Math.Clamp(ballPosOpenTK.Y, rect.Min.Y, rect.Max.Y);

        // 2. Calculate the distance vector between the closest point and the ball's center
        Vector2 distanceOpenTK = ballPosOpenTK - closestPointOpenTK;

        // 3. Check if the squared distance is less than the squared radius
        bool result = distanceOpenTK.LengthSquared <= (ball.Radius * ball.Radius);

        // Convert the closest point back to C# Numerics Vector2 for the 'out' parameter
        closestPoint = new SysVector2(closestPointOpenTK.X, closestPointOpenTK.Y);
        return result;
    }

    // Simplified collision response (reverses velocity based on which axis was hit hardest)
    public static void ResolveCollision(BallObject ball, Box2 rect, SysVector2 closestPoint)
    {
        SysVector2 difference = ball.Position - closestPoint;

        // If the ball center and the closest point are the same (perfect center hit), avoid division by zero
        if (difference.LengthSquared() == 0)
        {
            // Simple fallback: just reverse Y velocity
            ball.Velocity.Y *= -1;
            return;
        }

        // Calculate the vector needed to push the ball out of the rectangle (if it penetrated)
        // System.Numerics.Vector2.Normalize is used
        SysVector2 penetration = SysVector2.Normalize(difference) * (ball.Radius - difference.Length());

        // Move the ball back to resolve the penetration
        ball.Position += penetration;

        // Determine which axis needs velocity reversal
        if (Math.Abs(penetration.X) > Math.Abs(penetration.Y))
        {
            // Collision occurred more on the X-axis (side hit)
            ball.Velocity.X *= -1;
        }
        else
        {
            // Collision occurred more on the Y-axis (top/bottom hit)
            ball.Velocity.Y *= -1;
        }
    }
}

// --- MAIN GAME WINDOW ---
public class Game : GameWindow
{
    private BallObject ball;
    private GameObject paddle;
    // private GameObject floor; // Floor removed
    private List<GameObject> bricks = new List<GameObject>();
    private Renderer renderer;

    public Game() : base(GameWindowSettings.Default, new NativeWindowSettings()
    {
        Size = new Vector2i(Config.ScreenWidth, Config.ScreenHeight),
        Title = Config.Title,
        API = ContextAPI.OpenGL,
        Profile = ContextProfile.Core,
        APIVersion = new Version(3, 3) // Set minimum OpenGL version to 3.3 for core profile
    })
    {
        // Center the window on the screen
        CenterWindow();
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        // OpenGL Initialization
        GL.ClearColor(0.08f, 0.08f, 0.16f, 1.0f); // Dark background

        renderer = new Renderer();
        InitializeGame();
    }

    private void InitializeGame()
    {
        // 1. Initialize Ball
        SysVector2 initialBallPos = new SysVector2(Config.ScreenWidth / 2f, Config.ScreenHeight / 2f); // Start higher up
        SysVector2 initialVelocity = new SysVector2(150f, -250f); // Pixels per second
        ball = new BallObject(initialBallPos, 8f, initialVelocity);

        // 2. Initialize Paddle (Positioned near the bottom of the screen)
        SysVector2 paddleSize = new SysVector2(Config.ScreenWidth / 5f, 20);
        // Positioned 40 pixels up from the bottom edge
        SysVector2 paddlePos = new SysVector2(Config.ScreenWidth / 2f - paddleSize.X / 2f, Config.ScreenHeight - 40);
        paddle = new GameObject(paddlePos, paddleSize, new Vector4(0.5f, 0.8f, 1.0f, 1.0f)); // Sky Blue

        // 3. Initialize Floor (REMOVED)
        // floor = new GameObject(floorPos, floorSize, new Vector4(0.3f, 0.3f, 0.3f, 1.0f)); 

        // 4. Initialize Bricks (Level)
        SetupBricks(5, 8); // 5 rows, 8 columns
    }

    private void ResetBall()
    {
        // Reset ball to a default, centered position
        ball.Position = new SysVector2(Config.ScreenWidth / 2f, Config.ScreenHeight / 2f);
        // Reset velocity (downwards to start game again)
        ball.Velocity = new SysVector2(150f, -250f);
        System.Console.WriteLine("Game Over: Ball fell through the bottom. Resetting...");
        // NOTE: In a full game, this is where you'd decrement lives or show a game over screen.
    }

    private void SetupBricks(int rows, int cols)
    {
        float brickWidth = 30f;
        float brickHeight = 10f;
        float padding = 5f;
        float offsetX = (Config.ScreenWidth - (cols * brickWidth + (cols - 1) * padding)) / 2f;
        float offsetY = 50f;

        for (int r = 0; r < rows; r++)
        {
            Vector4 color = r % 2 == 0 ? new Vector4(1.0f, 0.2f, 0.2f, 1.0f) : new Vector4(1.0f, 0.6f, 0.2f, 1.0f); // Red/Orange
            for (int c = 0; c < cols; c++)
            {
                SysVector2 pos = new SysVector2(
                    offsetX + c * (brickWidth + padding),
                    offsetY + r * (brickHeight + padding)
                );
                bricks.Add(new GameObject(pos, new SysVector2(brickWidth, brickHeight), color));
            }
        }
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        float dt = (float)args.Time;

        // 1. Handle Paddle Movement
        HandlePaddleInput(dt);

        // 2. Update Ball Position
        ball.Update(dt);

        // 3. Check Collisions
        CheckAllCollisions();

        // Check if the window is closed
        if (KeyboardState.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Escape))
        {
            Close();
        }
    }

    private void HandlePaddleInput(float dt)
    {
        float paddleSpeed = 400f; // Pixels per second
        float dx = 0f;

        // ONLY LEFT ARROW KEY 
        if (KeyboardState.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Left))
        {
            dx -= paddleSpeed * dt;
        }
        // ONLY RIGHT ARROW KEY
        if (KeyboardState.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Right))
        {
            dx += paddleSpeed * dt;
        }

        // Get current Min X position
        float currentMinX = paddle.Rect.Min.X + dx;

        // Clamp paddle position to screen bounds
        float paddleWidth = paddle.Rect.Size.X;
        float minX = Math.Clamp(currentMinX, 0, Config.ScreenWidth - paddleWidth);

        // Get the constant Y position (top edge of the paddle)
        float paddleY = paddle.Rect.Min.Y;

        // CALCULATE THE CORRECT NEW BOUNDING BOX (FIXED LOGIC)
        Vector2 newMin = new Vector2(minX, paddleY);
        Vector2 newMax = new Vector2(minX + paddleWidth, paddleY + paddle.Rect.Size.Y);

        // Update the Box2 Rect 
        paddle.Rect = new Box2(newMin, newMax);
    }

    private void CheckAllCollisions()
    {
        SysVector2 closestPoint;

        // --- AABB-AABB Screen Edge Collision (for the ball) ---
        // Top Wall
        if (ball.Position.Y - ball.Radius <= 0)
        {
            ball.Velocity.Y *= -1;
            ball.Position.Y = ball.Radius;
        }

        // Left/Right Walls
        if (ball.Position.X - ball.Radius <= 0 || ball.Position.X + ball.Radius >= Config.ScreenWidth)
        {
            ball.Velocity.X *= -1;
            // Ensure it doesn't stick
            if (ball.Position.X - ball.Radius < 0) ball.Position.X = ball.Radius;
            if (ball.Position.X + ball.Radius > Config.ScreenWidth) ball.Position.X = Config.ScreenWidth - ball.Radius;
        }

        // --- GAME OVER: Ball fell through the bottom ---
        if (ball.Position.Y + ball.Radius >= Config.ScreenHeight)
        {
            ResetBall();
            return;
        }

        // --- AABB-Circle Paddle Collision (Paddle vs Ball) ---
        if (Collision.CheckCollisionCircleAABB(ball, paddle.Rect, out closestPoint))
        {
            // Simple bounce response (AABB-Circle required logic)
            Collision.ResolveCollision(ball, paddle.Rect, closestPoint);

            // Optionally, add a slight velocity boost based on where the paddle was hit
            float centerDiff = (ball.Position.X - paddle.Rect.Min.X) - (paddle.Rect.Size.X / 2);
            ball.Velocity.X += centerDiff * 1.5f;

            // Re-normalize speed (using C# Numerics SysVector2 methods)
            ball.Velocity = SysVector2.Normalize(ball.Velocity) * ball.Velocity.Length();
        }

        // --- AABB-Circle Brick Collisions (Bricks vs Ball) ---
        for (int i = 0; i < bricks.Count; i++)
        {
            GameObject brick = bricks[i];
            if (brick.IsDestroyed) continue;

            if (Collision.CheckCollisionCircleAABB(ball, brick.Rect, out closestPoint))
            {
                // Mark as destroyed and apply bounce
                brick.IsDestroyed = true;
                Collision.ResolveCollision(ball, brick.Rect, closestPoint);
            }
        }
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.Clear(ClearBufferMask.ColorBufferBit);

        // Draw Bricks
        foreach (var brick in bricks)
        {
            brick.Draw(renderer);
        }

        // Draw Paddle
        paddle.Draw(renderer);

        // Draw Floor (REMOVED)
        // floor.Draw(renderer);

        // Draw Ball (now rendered as a square)
        ball.Draw(renderer);

        SwapBuffers();
    }

    // Required Main method
    public static void Main(string[] args)
    {
        using (Game game = new Game())
        {
            game.Run();
        }
    }
}