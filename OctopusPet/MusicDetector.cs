using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace OctopusPet;

/// <summary>
/// 通过 NAudio 检测所有活跃音频设备的输出音量。
/// 支持扬声器、耳机、蓝牙设备等所有音频输出。
/// 每次检测时刷新设备列表，确保新插入的设备能被识别。
/// </summary>
public sealed class MusicDetector : IDisposable
{
    private readonly MMDeviceEnumerator? _enumerator;
    private readonly bool _initFailed;

    public MusicDetector()
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();
            App.Log("MusicDetector: initialized");
        }
        catch (Exception ex)
        {
            _initFailed = true;
            App.Log("MusicDetector init failed: " + ex.Message);
        }
    }

    /// <summary>所有活跃设备中的最大峰值音量（0~1）；检测不可用时返回 0。</summary>
    public float GetPeak()
    {
        if (_initFailed) return 0f;

        float maxPeak = 0f;
        MMDeviceCollection? devices = null;

        try
        {
            // 每次都重新枚举设备，确保新插入的耳机能被识别
            devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (var device in devices)
            {
                try
                {
                    float peak = device.AudioMeterInformation.MasterPeakValue;
                    if (peak > maxPeak) maxPeak = peak;
                }
                catch
                {
                    // 忽略单个设备的检测失败
                }
                finally
                {
                    device.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            App.Log("MusicDetector GetPeak failed: " + ex.Message);
        }
        finally
        {
            // MMDeviceCollection 没有 Dispose 方法，GC 会处理
        }

        return maxPeak;
    }

    public void Dispose()
    {
        _enumerator?.Dispose();
    }
}
