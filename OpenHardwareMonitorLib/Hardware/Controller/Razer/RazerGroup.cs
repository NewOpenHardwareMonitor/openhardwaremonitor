﻿using System.Collections.Generic;
using System.IO;
using System.Text;
using HidSharp;

namespace OpenHardwareMonitor.Hardware.Controller.Razer;

internal class RazerGroup : IGroup
{
    private readonly List<IHardware> _hardware = new();
    private readonly StringBuilder _report = new();

    public RazerGroup(ISettings settings)
    {
        _report.AppendLine("Razer Hardware");
        _report.AppendLine();

        foreach (HidDevice dev in DeviceList.Local.GetHidDevices(0x1532))
        {
            try
            {
                string productName = dev.GetProductName();

                switch (dev.ProductID)
                {
                    case 0x0F3C: // Razer PWM PC fan controller
                        if (dev.GetMaxFeatureReportLength() <= 0)
                            break;

                        var device = new RazerFanController(dev, settings);
                        _report.AppendLine($"Device name: {productName}");
                        _report.AppendLine($"Firmware version: {device.FirmwareVersion}");
                        _report.AppendLine($"{device.Status}");
                        _report.AppendLine();
                        _hardware.Add(device);
                        break;

                    default:
                        _report.AppendLine($"Unknown Hardware PID: {dev.ProductID} Name: {productName}");
                        _report.AppendLine();
                        break;
                }
            }
            catch (IOException e)
            {
                _report.AppendLine($"Failed to get information for USB device with id {dev.ProductID}: {e.Message} ");
            }
        }

        if (_hardware.Count == 0)
        {
            _report.AppendLine("No Razer Hardware found.");
            _report.AppendLine();
        }
    }

    public IReadOnlyList<IHardware> Hardware => _hardware;

    public void Close()
    {
        foreach (IHardware iHardware in _hardware)
        {
            if (iHardware is Hardware hardware)
                hardware.Close();
        }
    }

    public string GetReport()
    {
        return _report.ToString();
    }
}
