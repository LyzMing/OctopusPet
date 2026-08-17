using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Clipboard = System.Windows.Clipboard;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace OctopusPet;

public partial class MainWindow : Window
{
    // ---------- 可调参数 ----------
    private const int SpriteW = 146;                 // 精灵像素宽
    private const int SpriteH = 128;                 // 精灵像素高
    private const int TickMs = 30;                   // 动画帧间隔（约 33fps）
    private const double BodySwitchChance = 0.2;     // 常态每帧切换身体的概率（抖动强度）
    private const double SleepBodySwitchChance = 0.12; // 睡觉时抖动频率（略微减小）
    private const double WalkSpeedMin = 45;          // 移动速度下限 px/s
    private const double WalkSpeedMax = 95;          // 移动速度上限 px/s
    private const double IdleMinSec = 3.0;           // 停留最短时间
    private const double IdleMaxSec = 8.0;           // 停留最长时间
    private const bool DebugStill = false;           // 【调试】临时禁止游走，正常为 false
    private const double OpenEyeMinSec = 3.0;        // 睁眼最短时长
    private const double OpenEyeMaxSec = 7.0;        // 睁眼最长时长
    private const double ClosedEyeMinSec = 2.0;      // 闭眼（打盹）最短时长
    private const double ClosedEyeMaxSec = 4.0;      // 闭眼（打盹）最长时长
    private const double BlinkMsMin = 100;           // 眨眼最短时长
    private const double BlinkMsMax = 180;           // 眨眼最长时长
    private const double SleepChance = 0.35;         // 睁眼结束后进入闭眼的概率
    private const double TongueChance = 0.5;         // 闭眼期间触发吐舌头的概率
    private const double TongueSwingMs = 180;        // 舌头左右摆动间隔
    private const int SleepStartClosedMs = 600;      // 进入睡觉：常态闭眼时长
    private const int SleepStartTransMs = 600;       // 进入睡觉：过渡身体时长
    private const int WakingJustWokeMs = 600;        // 结束睡觉：刚醒眼睛时长
    private const int WakingSleepEyesMs = 400;       // 结束睡觉：睡觉眼睛时长
    private const int WakingTransMs = 600;           // 结束睡觉：过渡身体时长
    private const int WakingNormalClosedMs = 600;    // 结束睡觉：常态闭眼时长
    private const int ZzzMs = 1200;                  // 每个 z 的显示时长
    // ---------------------------------

    private enum EyeState { Open, Closed, Blink }
    private enum PetState
    {
        Normal,
        SleepStartClosed,    // 进入睡觉第1步：常态身体 + 闭眼
        SleepStartTrans,     // 进入睡觉第2步：过渡身体 + 过渡眼睛
        Sleeping,            // 睡觉：3 个睡觉身体抖动 + 睡觉眼睛 + zzz
        WakingJustWoke,      // 结束睡觉第1步：睡觉身体 + 刚醒眼睛
        WakingSleepEyes,     // 结束睡觉第2步：睡觉身体 + 睡觉眼睛
        WakingTrans,         // 结束睡觉第3步：过渡身体 + 过渡眼睛
        WakingNormalClosed,  // 结束睡觉第4步：常态身体 + 闭眼
    }

    private readonly Random _rnd = new();

    // 精灵图
    private readonly BitmapImage[] _nOpen = new BitmapImage[3];     // 常态睁眼
    private readonly BitmapImage[] _nClosed = new BitmapImage[3];   // 常态闭眼
    private readonly BitmapImage[] _nExcited = new BitmapImage[3];  // 激动眼睛（拖动时）
    private readonly BitmapImage[] _nT1 = new BitmapImage[3];       // 闭眼+舌头1
    private readonly BitmapImage[] _nT2 = new BitmapImage[3];       // 闭眼+舌头2
    private BitmapImage _trans = null!;                             // 过渡身体+眼睛
    private readonly BitmapImage[] _sleep = new BitmapImage[3];     // 睡觉身体+睡觉眼睛
    private readonly BitmapImage[] _woke = new BitmapImage[3];      // 睡觉身体+刚醒眼睛

    private PetState _petState = PetState.Normal;
    private int _body = 0;
    private bool _facingLeft;

    // 眼睛状态机
    private EyeState _eyeState = EyeState.Open;
    private DateTime _eyeStateUntil = DateTime.MinValue;
    private DateTime _nextBlinkAt = DateTime.MinValue;

    // 舌头
    private bool _tongueActive;
    private int _tongueFrame;
    private DateTime _tongueNext = DateTime.MinValue;

    // 阶段（进入/结束睡觉的过渡、zzz）
    private DateTime _phaseUntil = DateTime.MinValue;
    private int _zzzStage = 0;
    private DateTime _zzzUntil = DateTime.MinValue;

    // 移动
    private bool _moving;
    private Point _target;
    private double _speed;
    private DateTime _idleUntil = DateTime.MinValue;

    // 拖拽
    private bool _dragging;

    private readonly DispatcherTimer _timer;
    private DateTime _lastTick = DateTime.UtcNow;

    public MainWindow()
    {
        App.Log("MainWindow ctor start");
        InitializeComponent();
        LoadSprites();
        App.Log("MainWindow ctor sprites loaded");

        // 初始随机位置（工作区右下区域）
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - SpriteW - 40 - _rnd.NextDouble() * 200;
        Top = wa.Bottom - SpriteH - 20 - _rnd.NextDouble() * 120;
        ClampToWorkArea();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
        _timer.Tick += OnTick;
        _timer.Start();
        _lastTick = DateTime.UtcNow;

        ScheduleIdle();
        ScheduleEyeOpen();
        App.Log("MainWindow ctor done");
    }

    protected override void OnClosed(EventArgs e)
    {
        App.Log("MainWindow.OnClosed");
        base.OnClosed(e);
    }

    // ---------- 精灵加载 ----------
    private void LoadSprites()
    {
        for (int b = 0; b < 3; b++)
        {
            _nOpen[b] = Load($"sprites/n_open{b + 1}.png");
            _nClosed[b] = Load($"sprites/n_closed{b + 1}.png");
            _nExcited[b] = Load($"sprites/n_excited{b + 1}.png");
            _nT1[b] = Load($"sprites/n_t1_{b + 1}.png");
            _nT2[b] = Load($"sprites/n_t2_{b + 1}.png");
            _sleep[b] = Load($"sprites/sleep{b + 1}.png");
            _woke[b] = Load($"sprites/woke{b + 1}.png");
        }
        _trans = Load("sprites/trans.png");
        Z1Img.Source = Load("sprites/z1.png");
        Z2Img.Source = Load("sprites/z2.png");
        Z3Img.Source = Load("sprites/z3.png");
        PetImage.Source = _nOpen[0];
    }

    private static BitmapImage Load(string name)
    {
        var uri = new Uri($"pack://application:,,,/{name}", UriKind.Absolute);
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.UriSource = uri;
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    // ---------- 主循环 ----------
    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        switch (_petState)
        {
            case PetState.Normal:
                UpdateBody(BodySwitchChance);
                UpdateEyes(now);
                UpdateTongue(now);
                UpdateMovement(now, dt);
                break;

            case PetState.SleepStartClosed:
                UpdateBody(BodySwitchChance);
                if (now >= _phaseUntil)
                {
                    _petState = PetState.SleepStartTrans;
                    _phaseUntil = now.AddMilliseconds(SleepStartTransMs);
                }
                break;

            case PetState.SleepStartTrans:
                if (now >= _phaseUntil) EnterSleeping(now);
                break;

            case PetState.Sleeping:
                UpdateBody(SleepBodySwitchChance);
                UpdateZzz(now);
                break;

            case PetState.WakingJustWoke:
                UpdateBody(SleepBodySwitchChance);
                if (now >= _phaseUntil)
                {
                    _petState = PetState.WakingSleepEyes;
                    _phaseUntil = now.AddMilliseconds(WakingSleepEyesMs);
                }
                break;

            case PetState.WakingSleepEyes:
                UpdateBody(SleepBodySwitchChance);
                if (now >= _phaseUntil)
                {
                    _petState = PetState.WakingTrans;
                    _phaseUntil = now.AddMilliseconds(WakingTransMs);
                }
                break;

            case PetState.WakingTrans:
                if (now >= _phaseUntil)
                {
                    _petState = PetState.WakingNormalClosed;
                    _phaseUntil = now.AddMilliseconds(WakingNormalClosedMs);
                }
                break;

            case PetState.WakingNormalClosed:
                UpdateBody(BodySwitchChance);
                if (now >= _phaseUntil)
                {
                    _petState = PetState.Normal;
                    ScheduleIdle();
                    ScheduleEyeOpen();
                }
                break;
        }

        ApplySprite();
    }

    // 身体：随机切换 3 帧，制造抖动效果
    private void UpdateBody(double chance)
    {
        if (_rnd.NextDouble() < chance)
        {
            int nb;
            do { nb = _rnd.Next(3); } while (nb == _body);
            _body = nb;
        }
    }

    // 眼睛：睁眼 / 闭眼 / 眨眼 状态机（仅常态运行）
    private void UpdateEyes(DateTime now)
    {
        switch (_eyeState)
        {
            case EyeState.Open:
                if (_nextBlinkAt != DateTime.MinValue && now >= _nextBlinkAt)
                {
                    _eyeState = EyeState.Blink;
                    _eyeStateUntil = now.AddMilliseconds(BlinkMsMin + _rnd.NextDouble() * (BlinkMsMax - BlinkMsMin));
                }
                else if (now >= _eyeStateUntil)
                {
                    if (_rnd.NextDouble() < SleepChance)
                    {
                        _eyeState = EyeState.Closed;
                        _eyeStateUntil = now.AddSeconds(ClosedEyeMinSec + _rnd.NextDouble() * (ClosedEyeMaxSec - ClosedEyeMinSec));
                        // 进入闭眼（打盹）时按概率触发吐舌头
                        _tongueActive = _rnd.NextDouble() < TongueChance;
                        _tongueFrame = 0;
                        _tongueNext = now.AddMilliseconds(TongueSwingMs);
                    }
                    else
                    {
                        ScheduleEyeOpen();
                    }
                }
                break;

            case EyeState.Blink:
                if (now >= _eyeStateUntil) ScheduleEyeOpen();
                break;

            case EyeState.Closed:
                if (now >= _eyeStateUntil)
                {
                    _tongueActive = false;
                    ScheduleEyeOpen();
                }
                break;
        }
    }

    private void ScheduleEyeOpen()
    {
        _eyeState = EyeState.Open;
        _eyeStateUntil = DateTime.UtcNow.AddSeconds(OpenEyeMinSec + _rnd.NextDouble() * (OpenEyeMaxSec - OpenEyeMinSec));
        _nextBlinkAt = DateTime.UtcNow.AddSeconds(2 + _rnd.NextDouble() * 3.5);
    }

    // 舌头摆动：两个舌头图层来回切换
    private void UpdateTongue(DateTime now)
    {
        if (!_tongueActive) return;
        if (now >= _tongueNext)
        {
            _tongueFrame ^= 1;
            _tongueNext = now.AddMilliseconds(TongueSwingMs);
        }
    }

    // ---------- 睡觉 / 醒来 ----------
    private void StartSleep()
    {
        if (_petState != PetState.Normal) return;
        _moving = false;
        _tongueActive = false;
        _eyeState = EyeState.Closed;   // 先切换成常态闭眼
        _petState = PetState.SleepStartClosed;
        _phaseUntil = DateTime.UtcNow.AddMilliseconds(SleepStartClosedMs);
        HideZzz();
    }

    private void EnterSleeping(DateTime now)
    {
        _petState = PetState.Sleeping;
        _phaseUntil = DateTime.MinValue;
        _zzzStage = 0;
        _zzzUntil = now.AddMilliseconds(ZzzMs);
        ShowZzzStage();
    }

    private void StartWake()
    {
        if (_petState != PetState.Sleeping) return;
        HideZzz();                       // 停止显示 zzz
        _tongueActive = false;
        _petState = PetState.WakingJustWoke;
        _phaseUntil = DateTime.UtcNow.AddMilliseconds(WakingJustWokeMs);
    }

    // zzz：z1 -> z2 -> z3 循环
    private void UpdateZzz(DateTime now)
    {
        if (now >= _zzzUntil)
        {
            _zzzStage = (_zzzStage + 1) % 3;
            _zzzUntil = now.AddMilliseconds(ZzzMs);
        }
        ShowZzzStage();
    }

    private void ShowZzzStage()
    {
        Z1Img.Visibility = _zzzStage == 0 ? Visibility.Visible : Visibility.Hidden;
        Z2Img.Visibility = _zzzStage == 1 ? Visibility.Visible : Visibility.Hidden;
        Z3Img.Visibility = _zzzStage == 2 ? Visibility.Visible : Visibility.Hidden;
    }

    private void HideZzz()
    {
        Z1Img.Visibility = Z2Img.Visibility = Z3Img.Visibility = Visibility.Hidden;
    }

    // ---------- 移动：在屏幕内随机游走 / 停留 ----------
    private void UpdateMovement(DateTime now, double dt)
    {
        if (_dragging) return;

        var wa = SystemParameters.WorkArea;

        if (_moving)
        {
            double dx = _target.X - Left;
            double dy = _target.Y - Top;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 12)
            {
                _moving = false;
                ScheduleIdle();
                return;
            }
            double step = _speed * dt;
            if (step >= dist) step = dist;
            Left += dx / dist * step;
            Top += dy / dist * step;
            ClampToWorkArea();

            // 朝向：往左走就朝左（镜像翻转）
            bool wantLeft = dx < -1;
            if (wantLeft != _facingLeft) SetFacing(wantLeft);
        }
        else if (now >= _idleUntil)
        {
            double tx = wa.Left + _rnd.NextDouble() * Math.Max(10, wa.Width - SpriteW);
            double ty = wa.Top + _rnd.NextDouble() * Math.Max(10, wa.Height - SpriteH);
            _target = new Point(tx, ty);
            _speed = WalkSpeedMin + _rnd.NextDouble() * (WalkSpeedMax - WalkSpeedMin);
            _moving = true;
        }
    }

    private void ScheduleIdle()
    {
        // 调试模式不游走：_idleUntil 设为极远，UpdateMovement 永远不会触发移动
        _idleUntil = DebugStill
            ? DateTime.UtcNow.AddYears(100)
            : DateTime.UtcNow.AddSeconds(IdleMinSec + _rnd.NextDouble() * (IdleMaxSec - IdleMinSec));
    }

    private void SetFacing(bool left)
    {
        _facingLeft = left;
        PetImage.RenderTransform = new ScaleTransform(left ? -1 : 1, 1, SpriteW / 2.0, 0);
    }

    // 根据当前状态选择精灵
    private void ApplySprite()
    {
        BitmapImage src;
        switch (_petState)
        {
            case PetState.Normal:
                if (_dragging) src = _nExcited[_body];          // 拖动时用激动眼睛
                else if (_eyeState == EyeState.Open) src = _nOpen[_body];
                else if (_tongueActive) src = _tongueFrame == 0 ? _nT1[_body] : _nT2[_body];
                else src = _nClosed[_body];
                break;
            case PetState.SleepStartClosed:
            case PetState.WakingNormalClosed:
                src = _nClosed[_body];
                break;
            case PetState.SleepStartTrans:
            case PetState.WakingTrans:
                src = _trans;
                break;
            case PetState.Sleeping:
            case PetState.WakingSleepEyes:
                src = _sleep[_body];
                break;
            case PetState.WakingJustWoke:
                src = _woke[_body];
                break;
            default:
                src = _nOpen[_body];
                break;
        }
        PetImage.Source = src;
    }

    private void ClampToWorkArea()
    {
        var wa = SystemParameters.WorkArea;
        Left = Math.Max(wa.Left, Math.Min(Left, wa.Right - SpriteW));
        Top = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - SpriteH));
    }

    // ---------- 拖拽 ----------
    // 左键拖动：移动章鱼；右键拖动：框选屏幕截图（章鱼跟随，表情同左键拖动）。
    // 用 Win32 的真实物理坐标（GetCursorPos / GetWindowRect）实现拖拽，
    // 避免 WPF 在透明窗口 + 高 DPI 下坐标空间不一致导致的抖动问题。
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rc);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private double _grabX, _grabY;   // 抓取点相对窗口左上角的偏移（物理像素）
    private double _scaleX, _scaleY; // 窗口物理像素 / WPF 单位(DIP) 的换算比例
    private MouseButton _dragButton; // 当前拖拽使用的鼠标键

    // 右键框选截图
    private bool _rightDragging;     // 正在右键拖拽（框选）
    private bool _rightMoved;        // 是否已超过"点击/拖动"阈值
    private POINT _rightStart;       // 框选起点（物理坐标，即按下点 = 左上角）
    private Window? _overlay;        // 框选虚线窗口

    private const double DragClickThresholdPx = 8.0; // 右键移动超过该距离视为框选拖动

    private void StartDrag(MouseButton button)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        GetCursorPos(out var c);
        GetWindowRect(hwnd, out var r);
        _scaleX = Math.Max(1e-6, (double)(r.Right - r.Left) / Math.Max(ActualWidth, 1e-6));
        _scaleY = Math.Max(1e-6, (double)(r.Bottom - r.Top) / Math.Max(ActualHeight, 1e-6));
        _grabX = c.X - r.Left;
        _grabY = c.Y - r.Top;
        _dragging = true;
        _dragButton = button;
        _moving = false;
        CaptureMouse();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 只有完全回到常态状态才能拖动（睡觉及过渡期间禁用）
        if (_petState != PetState.Normal)
        {
            e.Handled = true;
            return;
        }
        StartDrag(MouseButton.Left);
        e.Handled = true;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 只有常态才能框选截图（睡觉及过渡期间禁用，右键仍用于菜单）
        if (_petState != PetState.Normal)
        {
            e.Handled = true;
            return;
        }
        StartDrag(MouseButton.Right);
        GetCursorPos(out _rightStart);
        _rightDragging = true;
        _rightMoved = false;
        if (_overlay == null) _overlay = CreateOverlay();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        bool pressed = _dragButton == MouseButton.Left
            ? e.LeftButton == MouseButtonState.Pressed
            : e.RightButton == MouseButtonState.Pressed;
        if (!pressed) return;

        GetCursorPos(out var c);
        Left = (c.X - _grabX) / _scaleX;
        Top = (c.Y - _grabY) / _scaleY;
        ClampToWorkArea();

        if (_rightDragging)
        {
            double dx = c.X - _rightStart.X;
            double dy = c.Y - _rightStart.Y;
            if (!_rightMoved && (Math.Abs(dx) > DragClickThresholdPx || Math.Abs(dy) > DragClickThresholdPx))
                _rightMoved = true;
            if (_rightMoved) UpdateOverlay(c);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging && _dragButton == MouseButton.Left)
        {
            _dragging = false;
            ReleaseMouseCapture();
            ScheduleIdle(); // 放下后停留一会儿再走
            e.Handled = true;
        }
    }

    private async void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging && _dragButton == MouseButton.Right)
        {
            _dragging = false;
            _rightDragging = false;
            ReleaseMouseCapture();
            GetCursorPos(out var c);
            if (_rightMoved)
            {
                // 框选截图：隐藏章鱼和选框后截屏，复制到剪贴板
                await FinishCapture(_rightStart, c);
                ScheduleIdle();
            }
            else
            {
                HideOverlay();
                ShowContextMenu();
            }
        }
        else
        {
            // 非常态（睡觉等）或非拖拽的普通右键 → 打开菜单
            ShowContextMenu();
        }
        e.Handled = true;
    }

    // ---------- 框选覆盖窗口 ----------
    private Window CreateOverlay()
    {
        var win = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
            Width = 0,
            Height = 0,
            Visibility = Visibility.Hidden,
        };
        win.Content = new System.Windows.Shapes.Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(40, 0, 120, 255)),   // 半透明蓝色填充
            Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 90, 90)), // 红色虚线框
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 },
        };
        win.Show();
        win.Visibility = Visibility.Hidden; // 先创建但不可见
        return win;
    }

    private void UpdateOverlay(POINT b)
    {
        if (_overlay == null) return;
        double x = Math.Min(_rightStart.X, b.X);
        double y = Math.Min(_rightStart.Y, b.Y);
        double w = Math.Abs(b.X - _rightStart.X);
        double h = Math.Abs(b.Y - _rightStart.Y);
        _overlay.Left = x / _scaleX;
        _overlay.Top = y / _scaleY;
        _overlay.Width = w / _scaleX;
        _overlay.Height = h / _scaleY;
        _overlay.Visibility = Visibility.Visible;
    }

    private void HideOverlay()
    {
        if (_overlay != null) _overlay.Visibility = Visibility.Hidden;
    }

    // ---------- 截图到剪贴板 ----------
    private async System.Threading.Tasks.Task FinishCapture(POINT a, POINT b)
    {
        double x = Math.Min(a.X, b.X);
        double y = Math.Min(a.Y, b.Y);
        double w = Math.Abs(b.X - a.X);
        double h = Math.Abs(b.Y - a.Y);
        int screenW = GetSystemMetrics(0); // SM_CXSCREEN
        int screenH = GetSystemMetrics(1); // SM_CYSCREEN
        x = Math.Max(0, Math.Min(x, screenW - 1));
        y = Math.Max(0, Math.Min(y, screenH - 1));
        w = Math.Min(w, screenW - x);
        h = Math.Min(h, screenH - y);
        if (w < 4 || h < 4) { HideOverlay(); return; }

        // 隐藏章鱼和选框，等一帧让屏幕重绘，保证截图里没有章鱼
        Visibility = Visibility.Hidden;
        HideOverlay();
        await System.Threading.Tasks.Task.Delay(80);
        try
        {
            using var bmp = new System.Drawing.Bitmap((int)w, (int)h);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen((int)x, (int)y, 0, 0, bmp.Size);
            }
            var hbmp = bmp.GetHbitmap();
            try
            {
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                Clipboard.SetImage(src); // 复制到剪贴板
            }
            finally { DeleteObject(hbmp); }
        }
        catch (Exception ex)
        {
            App.Log("Capture failed: " + ex);
        }
        finally
        {
            Visibility = Visibility.Visible;
        }
    }

    // ---------- 右键菜单 ----------
    private void ShowContextMenu()
    {
        var menu = new ContextMenu();
        if (_petState == PetState.Normal)
        {
            var sleep = new MenuItem { Header = "睡觉" };
            sleep.Click += (_, _) => StartSleep();
            menu.Items.Add(sleep);
        }
        else if (_petState == PetState.Sleeping)
        {
            var wake = new MenuItem { Header = "结束睡觉" };
            wake.Click += (_, _) => StartWake();
            menu.Items.Add(wake);
        }
        var quit = new MenuItem { Header = "退出桌宠" };
        quit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(quit);
        menu.IsOpen = true;
    }
}
