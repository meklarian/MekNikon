using Nikon;
using System.Diagnostics;

namespace NikonController
{
    public class CaptureDevice : IDisposable
    {
        public CaptureDevice() { }

        NikonDevice? _device = null;
        NikonManager? _manager = null;

        NikonEnum? _shutterSpeedSet = null;
        Dictionary<string, int> _shutterSpeedIndexMap = new Dictionary<string, int>();
        NikonEnum? _apertureSet = null;
        Dictionary<string, int> _apertureIndexMap = new Dictionary<string, int>();
        NikonEnum? _isoSet = null;
        Dictionary<string, int> _isoIndexMap = new Dictionary<string, int>();

        AutoResetEvent _waitForDevice = new(false);
        AutoResetEvent _waitForCaptureComplete = new(false);
        AutoResetEvent _waitCapChanged = new(false);

        string currentDriver = string.Empty;
        string currentCamera = string.Empty;

        public void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }

        public static class Commands
        {
            public const string Connect = "connect";
            public const string Disconnect = "disconnect";
            public const string Caps = "caps";
            public const string Capture = "capture";
            public const string SetISO = "set_iso";
            public const string SetAperture = "set_aperture";
            public const string SetShutterSpeed = "set_shutter";
        }

        public void Dispatch(string cmdLine)
        {
            if (string.IsNullOrWhiteSpace(cmdLine)) 
            {
                return; 
            }

            var fullCmdParts = cmdLine.Split(' ');
            var cmd = fullCmdParts[0];
            var cmdArgs = fullCmdParts.Length > 1 ? new ArraySegment<string>(fullCmdParts, 1, fullCmdParts.Length - 1) : Array.Empty<string>();

            if(cmd.Equals(Commands.Connect))
            {
                Connect(cmdArgs);
                return;
            }

            if(cmd.Equals(Commands.Disconnect))
            {
                Disconnect();
                return;
            }

            if(cmd.Equals(Commands.Caps))
            {
                PrintCapabilities(cmdArgs);
                return;
            }

            if(cmd.Equals(Commands.Capture))
            {
                Capture();
                return;
            }

            if (cmd.Equals(Commands.SetISO))
            {
                SetISO(cmdArgs);
                return;
            }

            if (cmd.Equals(Commands.SetAperture))
            {
                SetAperture(cmdArgs);
                return;
            }

            if (cmd.Equals(Commands.SetShutterSpeed))
            {
                SetShutterSpeed(cmdArgs);
                return;
            }
        }

        public void Connect(ArraySegment<string> args)
        {
            if(args.Count < 1) { return; }
            var cameraModel = args[0];

            if(_device != null) { return; }
            if (string.IsNullOrWhiteSpace(cameraModel)) { return; }

            if(cameraModel.Equals(Cameras.D500))
            {
                currentCamera = Cameras.D500;
                currentDriver = Devices.D500;
            } 
            else
            if (cameraModel.Equals(Cameras.Z7))
            {
                currentCamera = Cameras.Z7;
                currentDriver = Devices.Z7;
            }
            else
            if (cameraModel.Equals(Cameras.Z7II))
            {
                currentCamera = Cameras.Z7II;
                currentDriver = Devices.Z7II;
            }

            bool thrown = false;
            try
            {
                _manager = new NikonManager(currentDriver);

                _manager.DeviceAdded += manager_DeviceAdded;

                _waitForDevice.WaitOne();

                if(_device == null)
                {
                    return;
                }

                _device.CapabilityValueChanged += _device_CapabilityValueChanged1;
            }
            catch (Exception ex)
            {
                thrown = true;
                Console.Error.WriteLine(ex.ToString());
            }
            finally
            {
                if((null == _device) || thrown)
                {
                    Disconnect();
                }
            }
        }

        public void Disconnect()
        {
            if (null != _manager) 
            {
                _manager.DeviceAdded -= manager_DeviceAdded;
                _manager.Shutdown();
            }
            _manager = null;
            _device = null;
            currentDriver = string.Empty;
            currentCamera = string.Empty;
        }

        protected void TryOperation(Action fnOperation, int retryMax = 5, int? backoffDelay = 500)
        {
            bool success = false;
            bool backoff = false;
            int attempts = 0;

            do
            {
                attempts++;
                if (backoff && backoffDelay.HasValue)
                {
                    Thread.Sleep(backoffDelay.Value);
                }

                try
                {
                    fnOperation();
                    success = true;
                }
                catch (NikonException nex)
                {
                    if (nex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
                    {
                        PrintTrace($"attempt #{attempts} device busy, retrying");
                        backoff = true;
                        continue;
                    }
                    else
                    {
                        throw;
                    }
                }
            } while (!success && (attempts < retryMax));
        }

        public void Capture()
        {
            if(null == _device) { return; }

            Action fnOperation = () =>
            {
                PrintTrace("capture started");

                // Capture
                _device.Capture();

                PrintTrace("capture in progress");

                // Wait for the capture to complete
                //_waitForCaptureComplete.WaitOne();
                //Thread.Sleep(110);

                PrintTrace("capture completed");
            };

            TryOperation(fnOperation);
        }

        //void _device_ImageReady(NikonDevice sender, NikonImage image)
        //{
        //    // Save captured image to disk
        //    string filename = "image" + ((image.Type == NikonImageType.Jpeg) ? ".jpg" : ".nef");

        //    using (FileStream s = new FileStream(filename, FileMode.Create, FileAccess.Write))
        //    {
        //        s.Write(image.Buffer, 0, image.Buffer.Length);
        //    }
        //}

        void _device_CaptureComplete(NikonDevice sender, int data)
        {
            PrintTrace("device capture completed");
            // Signal the the capture completed
            _waitForCaptureComplete.Set();
        }

        void manager_DeviceAdded(NikonManager sender, NikonDevice device)
        {
            if (_device == null)
            {
                // Save device
                _device = device;

                // Signal that we got a device
                _waitForDevice.Set();
            }
        }

        private void _device_CapabilityValueChanged1(NikonDevice sender, eNkMAIDCapability capability)
        {
            _waitCapChanged.Set();
        }

        void SetShutterSpeed(ArraySegment<string> args)
        {
            if (null == _device)
            {
                PrintTrace("no device present to set shutter speed");
                return;
            }

            if(args.Count == 0)
            {
                PrintTrace("no shutter speed specified");
                return;
            }

            string speed = args[0];

            Action fnOperation = () =>
            {
                InitShutterSpeeds();

                if (_shutterSpeedSet == null)
                {
                    PrintTrace($"shutter speed could not be set (never received presets from device)");
                    return;
                }

                var lookupSpeed = speed.ToLower();
                if (!_shutterSpeedIndexMap.ContainsKey(lookupSpeed))
                {
                    PrintTrace($"{speed} does not match a known shutter speed for this device");
                    return;
                }

                if (_shutterSpeedSet.Index != _shutterSpeedIndexMap[lookupSpeed])
                {
                    _shutterSpeedSet.Index = _shutterSpeedIndexMap[lookupSpeed];
                    _device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_ShutterSpeed, _shutterSpeedSet);

                    _waitCapChanged.WaitOne();

                    PrintTrace($"shutter speed has been set to {speed}");
                }
                else
                {
                    PrintTrace($"shutter speed is already set to {speed}");
                }
            };

            TryOperation(fnOperation);
        }

        void SetAperture(ArraySegment<string> args)
        {
            if (null == _device)
            {
                PrintTrace("no device present to set aperture");
                return;
            }

            if (args.Count == 0)
            {
                PrintTrace("no aperture specified");
                return;
            }

            string aperture = args[0];

            Action fnOperation = () =>
            {
                InitApertures();

                if (_apertureSet == null)
                {
                    PrintTrace($"aperture could not be set (never received presets from device)");
                    return;
                }

                var lookupAperture = aperture.ToLower();
                if (!_apertureIndexMap.ContainsKey(lookupAperture))
                {
                    PrintTrace($"{aperture} does not match a known aperture for this device");
                    return;
                }

                if (_apertureSet.Index != _apertureIndexMap[lookupAperture])
                {
                    _apertureSet.Index = _apertureIndexMap[lookupAperture];
                    _device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_Aperture, _apertureSet);

                    _waitCapChanged.WaitOne();

                    PrintTrace($"aperture has been set to f/{aperture}");
                }
                else
                {
                    PrintTrace($"aperture is already set to f/{aperture}");
                }
            };

            TryOperation(fnOperation);
        }

        void SetISO(ArraySegment<string> args)
        {
            if (null == _device)
            {
                PrintTrace("no device present to set iso (sensitivity)");
                return;
            }

            if (args.Count == 0)
            {
                PrintTrace("no iso specified");
                return;
            }

            string iso = args[0];

            Action fnOperation = () =>
            {
                InitISOs();

                if (_isoSet == null)
                {
                    PrintTrace($"iso could not be set (never received presets from device)");
                    return;
                }

                var lookupiso = iso.ToLower();
                if (!_isoIndexMap.ContainsKey(lookupiso))
                {
                    PrintTrace($"{iso} does not match a known iso for this device");
                    return;
                }

                if (_isoSet.Index != _isoIndexMap[lookupiso])
                {
                    _isoSet.Index = _isoIndexMap[lookupiso];
                    _device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_Sensitivity, _isoSet);

                    _waitCapChanged.WaitOne();

                    PrintTrace($"iso sensitivity has been set to {iso}");
                }
                else
                {
                    PrintTrace($"iso sensitivity has already been set to {iso}");
                }
            };

            TryOperation(fnOperation);
        }

        void InitShutterSpeeds()
        {
            if (_device is null)
            {
                PrintTrace("unable to enumerate shutter speeds as no device is present");
                return;
            }

            if (_shutterSpeedIndexMap.Any()) { return; }

            NikonEnum shutterSpeeds = _device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ShutterSpeed);

            if (shutterSpeeds == null)
            {
                PrintTrace("device failed to enumerate shutter speeds");
                return;
            }

            _shutterSpeedSet = shutterSpeeds;

            PrintTrace($"shutter speed index is {shutterSpeeds.Index}");
            PrintTrace($"available shutter speeds:");

            for (int i = 0; i < shutterSpeeds.Length; i++)
            {
                var item = shutterSpeeds[i];

                if (item == null)
                {
                    PrintTrace($"  shutter speed at index {i} is null!");
                    continue;
                }
                else
                {
                    PrintTrace($"  {item} ({i})");

                    var key = item.ToString()?.ToLower() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        if (!_shutterSpeedIndexMap.ContainsKey(key))
                        {
                            _shutterSpeedIndexMap.Add(key, i);
                        }
                    }
                }
            }
        }

        void InitApertures()
        {
            if (_device is null)
            {
                PrintTrace("unable to enumerate apertures as no device is present");
                return;
            }

            if (_apertureIndexMap.Any()) { return; }

            NikonEnum apertures = _device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_Aperture);

            if (apertures == null)
            {
                PrintTrace("device failed to enumerate apertures");
                return;
            }

            _apertureSet = apertures;

            PrintTrace($"aperture index is {apertures.Index}");
            PrintTrace($"available apertures:");

            for (int i = 0; i < apertures.Length; i++)
            {
                var item = apertures[i];

                if (item == null)
                {
                    PrintTrace($"  aperture at index {i} is null!");
                    continue;
                }
                else
                {
                    PrintTrace($"  {item} ({i})");

                    var key = item.ToString()?.ToLower() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        if (!_apertureIndexMap.ContainsKey(key))
                        {
                            _apertureIndexMap.Add(key, i);
                        }
                    }
                }
            }
        }

        void InitISOs()
        {
            if (_device is null)
            {
                PrintTrace("unable to enumerate ISOs as no device is present");
                return;
            }

            if (_isoIndexMap.Any()) { return; }

            NikonEnum isos = _device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_Sensitivity);

            if (isos == null)
            {
                PrintTrace("device failed to enumerate ISOs");
                return;
            }

            _isoSet = isos;

            PrintTrace($"iso index is {isos.Index}");
            PrintTrace($"available isos:");

            for (int i = 0; i < isos.Length; i++)
            {
                var item = isos[i];

                if (item == null)
                {
                    PrintTrace($"  iso at index {i} is null!");
                    continue;
                }
                else
                {
                    PrintTrace($"  {item} ({i})");

                    var key = item.ToString()?.ToLower() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        if (!_isoIndexMap.ContainsKey(key))
                        {
                            _isoIndexMap.Add(key, i);
                        }
                    }
                }
            }
        }

        void PrintCapabilities(ArraySegment<string> args)
        {
            bool verbose = args.Count == 0;

            DumpCapabilities(!verbose, args);
        }

        void DumpCapabilities(bool filter, ArraySegment<string> args)
        {
            if (_device is null)
            {
                PrintTrace("unable to enumerate capabilities as no device is present");
                return;
            }

            PrintTrace("enumerating capabilities");

            // Get 'info' struct for each supported capability
            NkMAIDCapInfo[] caps = _device.GetCapabilityInfo();
            bool first = true;

            // Iterate through all supported capabilities
            foreach (NkMAIDCapInfo cap in caps)
            {
                var capId = cap.ulID.ToString();
                var desc = cap.GetDescription();

                if (filter)
                {
                    var capIdLower = capId.ToLower();
                    var descLower = desc.ToLower();

                    if (!(capIdLower.Contains(args[0]) || descLower.Contains(args[0])))
                    {
                        continue;
                    }
                }

                if (!first)
                {
                    Console.WriteLine();
                }
                else
                {
                    first = false;
                }

                // Print ID, description and type
                Console.WriteLine(string.Format("  {0, -14}: {1}", "Id", cap.ulID.ToString()));
                Console.WriteLine(string.Format("  {0, -14}: {1}", "Description", cap.GetDescription()));
                Console.WriteLine(string.Format("  {0, -14}: {1}", "Type", cap.ulType.ToString()));

                // Try to get the capability value
                string? value = null;
                bool readable = cap.CanGet();

                // First, check if the capability is readable
                if (readable)
                {
                    // Choose which 'Get' function to use, depending on the type
                    switch (cap.ulType)
                    {
                        case eNkMAIDCapType.kNkMAIDCapType_Unsigned:
                            value = _device.GetUnsigned(cap.ulID).ToString();
                            break;

                        case eNkMAIDCapType.kNkMAIDCapType_Integer:
                            value = _device.GetInteger(cap.ulID).ToString();
                            break;

                        case eNkMAIDCapType.kNkMAIDCapType_String:
                            value = _device.GetString(cap.ulID);
                            break;

                        case eNkMAIDCapType.kNkMAIDCapType_Boolean:
                            value = _device.GetBoolean(cap.ulID).ToString();
                            break;

                            // Note: There are more types - adding the rest is left
                            //       as an exercise for the reader.
                    }
                    Console.WriteLine(string.Format("  {0, -14}: {1}", "Value", null == value ? "(null)" : value));
                }
                else
                {
                    Console.WriteLine(string.Format("  {0, -14}: {1}", "Value Not Readable", "x"));
                }
            }

            PrintTrace("done enumerating capabilities");
        }

        void PrintTrace(string status)
        {
            DateTime dt = DateTime.Now;
            Console.WriteLine($"{dt.ToString("yyyy-MM-dd hh:mm:ss.fff")} {currentCamera}: {status}");
        }

        void DebugTrace(string status)
        {
            DateTime dt = DateTime.Now;
            Debug.WriteLine($"{dt.ToString("yyyy-MM-dd hh:mm:ss.fff")} {currentCamera}: {status}");
        }

        void MapDriverToCamera()
        {
            switch (currentDriver)
            {
                case Nikon.Devices.Z7II:
                    currentCamera = "z7ii";
                    break;
                case Nikon.Devices.D500:
                    currentCamera = "d500";
                    break;
                default:
                    currentCamera = "???";
                    break;
            }
        }
    }
}
