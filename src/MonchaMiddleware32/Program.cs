
#region Usings
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MonchaCommonBase;
#endregion

namespace MonchaController32 {

    /*
       This is a 32-bit process that calls C++ DLL functions from StclDevices.h to control MonchaNET laser controllers.
       It servers as kind of middleware for vvvv to mainly call the 32-bit DLL from 64-bit vvvv instances.
       Communication is done trough a local network socket. This program is invoked by vvvv (32- and 64-bit).
    */

    #region Delegates
    public delegate void OnNetworkPacketReceived(NetworkPacket packet);
    #endregion

    public class Program {

        #region Fields
        private bool loaded = false;
        private NetworkServer server;
        private List<LaserController> controllers;
        #endregion

        #region P/Invoke
        [DllImport("StclDevices.dll", CallingConvention = CallingConvention.StdCall)]                                    // OK
        public static extern int OpenDll();
        [DllImport("StclDevices.dll", CallingConvention = CallingConvention.StdCall)]                                    // OK
        public static extern void CloseDll();
        [DllImport("StclDevices.dll", CallingConvention = CallingConvention.StdCall)]                                    // OK
        public static extern int SearchForNETDevices(ref UInt32 pNumOfFoundDevs);
        [DllImport("StclDevices.dll", CallingConvention = CallingConvention.StdCall)]                                    // OK
        public static extern int CreateDeviceList(ref UInt32 pDeviceCount);
        [DllImport("StclDevices.dll", CallingConvention = CallingConvention.StdCall)]                                    // OK
        public static extern int GetDeviceIdentifier(UInt32 deviceIndex, out IntPtr ppDeviceName);
        [DllImport("StclDevices.dll", CallingConvention = CallingConvention.StdCall)]                                    // OK
        public static extern int SendFrame(UInt32 deviceIndex, HwLaserPoint[] pData, UInt32 numOfPoints, UInt32 scanrate);
        [DllImport("StclDevices.dll", CallingConvention = CallingConvention.StdCall)]                                    // OK
        public static extern int CanSendNextFrame(UInt32 deviceIndex, ref bool pCanSend);
        [DllImport("StclDevices.dll", CallingConvention = CallingConvention.StdCall)]                                    // TEST
        public static extern int SendDMX(UInt32 deviceIndex, byte[] pDMX);
        [DllImport("StclDevices.dll", CallingConvention = CallingConvention.StdCall)]                                    // OK
        public static extern int SendBlank(UInt32 deviceIndex, UInt16 x, UInt16 y);
        [DllImport("StclDevices.dll", CallingConvention = CallingConvention.StdCall)]                                    // OK
        public static extern int GetDeviceInfo(UInt32 deviceIndex, ref HwDeviceInfo pDeviceInfo);
        #endregion

        #region Structs
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HwLaserPoint {
            public UInt16 x;
            public UInt16 y;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] colors;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct HwDeviceInfo {

            public UInt32 maxScanrate;
            public UInt32 minScanrate;
            public UInt32 maxNumOfPoints;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string type;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string serial;

        }
        #endregion

        #region Lifecycle
        private Program() {
            //Debugger.Launch();
        }

        public void launch(int port) {
            server = new NetworkServer(port);
            server.onNetworkPacketReceived+=networkPacketReceived;
            server.launch();
        }
        #endregion

        #region Network
        public void networkPacketReceived(NetworkPacket packet) {
            if(packet is ManagementPacket) {
                #region Management
                ManagementPacket parcel = (ManagementPacket)packet;
                if(parcel.OpenDll) {
                    if(!loaded) {
                        if(OpenDll()==0) {
                            initDevices();
                            loaded=true;
                        } else {
                            if(server!=null) server.sendMessage("ERROR: Could not open DLL. Please check your paths.");
                        }
                    }
                } else {
                    if(loaded) {
                        setBlank();
                        CloseDll();
                        loaded=false;
                        if(server!=null) {
                            server.onNetworkPacketReceived-=networkPacketReceived;
                            server.terminate();
                            server=null;
                        }
                    }
                }
                #endregion
            } else if(packet is SearchDevicesPacket) {
                #region Search Controllers
                if(loaded) {
                    searchDevices();
                }
                #endregion
            } else if(packet is SendBlankPointPacket) {
                #region Send Blank Point
                if(loaded) {
                    //sendBlankPoint((SendBlankPointPacket)packet);
                    sendBlackPoint((SendBlankPointPacket)packet);
                }
                #endregion
            } else if(packet is SendLaserPointPacket) {
                #region Send Laser Points
                if(loaded) {
                    sendLaserPoints((SendLaserPointPacket)packet);
                }
                #endregion
            } else {
                #region Report Back
                if(server!=null) server.sendMessage("ERROR: Unknown packet type received on TCP socket.");
                #endregion
            }
        }
        #endregion

        #region Functions
        private UInt32 findDeviceIndex(string address) {
            for(int i = 0; i<controllers.Count; i++) {
                if(controllers[i].Address.Equals(address)) {
                    return controllers[i].Index;
                }
            }
            throw new InvalidDeviceException();
        }

        private void setBlank() {
            for(int i=0; i<controllers.Count; i++) {
                SendBlank((UInt32)controllers[i].Index, 0, 0);
            }
        }

        private void initDevices() {
            if(server!=null) {
                server.sendMessage("INFO: Initialize laser controllers stored in devices.ini file ...");
                UInt32 deviceCount = 0;
                int code = CreateDeviceList(ref deviceCount);
                if(code==0) {
                    server.sendMessage("INFO: Created list with "+deviceCount+" laser controllers found in devices.ini file.");
                    this.controllers = new List<LaserController>();
                    for(UInt32 i = 0; i<deviceCount; i++) {
                        try {
                            LaserController controller = new LaserController();
                            controller.Index = i;
                            queryDeviceAddress(controller);
                            queryDeviceProperties(controller);
                            controllers.Add(controller);
                            server.sendMessage("INFO: Added "+controller.Address+" at index "+i);
                        } catch(InvalidDeviceException) {
                            server.sendMessage("WARNING: Query device for address with index "+i+" failed.");
                            //server.sendExceptionCode(???, 3, getErrorCode(3)+" [M:3]");
                        }
                    }
                    // send device information to parent process
                    SearchDevicesResultPacket parcel = new SearchDevicesResultPacket();
                    parcel.LaserControllers = controllers;
                    server.send(parcel);
                } else {
                    server.sendMessage("WARNING: Could not create laser controller list ["+getErrorCode(code)+"]");
                }
            }
        }

        private void searchDevices() {
            if(server!=null) {
                server.sendMessage("INFO: Searching for laser controllers on LAN ...");
                UInt32 deviceCount = 0;
                int code = SearchForNETDevices(ref deviceCount);
                server.sendMessage("INFO: Found "+deviceCount+" laser controllers on LAN. Storing to device.ini file.");
                if(code==0) {
                    initDevices();
                } else {
                    server.sendMessage("WARNING: Could not search for laser controllers on LAN ["+getErrorCode(code)+"]");
                }
            }
        }

        private void sendBlankPoint(SendBlankPointPacket parcel) {
            if(server!=null) {
                try {
                    UInt32 index = findDeviceIndex(parcel.DeviceAddress);
                    int code = SendBlank(index, (UInt16)parcel.X, (UInt16)parcel.Y);
                    if(code!=0) {
                        server.sendExceptionCode(parcel.DeviceAddress, code, getErrorCode(code)+" [A:"+code+"]");
                    }
                } catch(InvalidDeviceException) {
                    server.sendExceptionCode(parcel.DeviceAddress, 3, getErrorCode(3)+" [G:3]");
                }
            }
        }

        private void sendBlackPoint(SendBlankPointPacket parcel) {
            if(server!=null) {
                try {
                    UInt32 index = findDeviceIndex(parcel.DeviceAddress);
                    // test device for overflowing
                    bool canSend = false;
                    int code1 = CanSendNextFrame(index, ref canSend);
                    if(code1==0) {
                        if(canSend) {
                            HwLaserPoint[] hwPoints = new HwLaserPoint[1];
                            hwPoints[0].x = (UInt16)parcel.X;
                            hwPoints[0].y = (UInt16)parcel.Y;
                            hwPoints[0].colors = new byte[] { 0, 0, 0, 0, 0, 0 };
                            // send laser points to device
                            int code2 = SendFrame(index, hwPoints, (UInt32)hwPoints.Length, (UInt32)parcel.DeviceScanrate);
                            #region Debug Output
                            /*for(int i=0; i<hwPoints.Length; i++) {
                                server.sendMessage("INFO: Sending x="+hwPoints[i].x+" y="+hwPoints[i].y+" c={"+hwPoints[i].colors[0]+", "+hwPoints[i].colors[1]+", "+hwPoints[i].colors[2]+", "+hwPoints[i].colors[3]+", "+hwPoints[i].colors[4]+", "+hwPoints[i].colors[5]+"} scanrate="+parcel.DeviceScanrate);
                            }*/
                            #endregion
                            if(code2!=0) {
                                //server.sendMessage("WARNING: Sending black points to device with index "+parcel.DeviceAddress+" failed: "+getErrorCode(code2));
                                server.sendExceptionCode(parcel.DeviceAddress, code2, getErrorCode(code2)+" [C:"+code2+"]");
                            }
                        } else {
                            server.sendExceptionCode(parcel.DeviceAddress, 5, "OVERFLOW");
                        }
                    } else {
                        //server.sendMessage("WARNING: Could not query laser device with index "+parcel.DeviceAddress+" for overflow state: "+getErrorCode(code1));
                        server.sendExceptionCode(parcel.DeviceAddress, code1, getErrorCode(code1)+" [B:"+code1+"]");
                    }
                } catch(InvalidDeviceException) {
                    server.sendExceptionCode(parcel.DeviceAddress, 3, getErrorCode(3)+" [H:3]");
                }
            }
        }

        bool written = false;

        private void sendLaserPoints(SendLaserPointPacket parcel) {
            if(server!=null) {
                try {
                    UInt32 index = findDeviceIndex(parcel.DeviceAddress);
                    // test device for overflowing
                    bool canSend = false;
                    int code1 = CanSendNextFrame(index, ref canSend);
                    if(code1==0) {
                        if(canSend) {
                            List<LaserPoint> netPoints = parcel.LaserPoints;
                            HwLaserPoint[] hwPoints = new HwLaserPoint[parcel.LaserPoints.Count];
                            // transform from network point to device point
                            for(int p=0; p<parcel.LaserPoints.Count; p++) {

                                double x = 32767.5*Toolkit.clamp(netPoints[p].X, -1.0, 1.0)+32767.5; Toolkit.clamp(ref x, 0.0, 65535.0);
                                double y = 32767.5*Toolkit.clamp(netPoints[p].Y, -1.0, 1.0)+32767.5; Toolkit.clamp(ref y, 0.0, 65535.0);
                                byte r = netPoints[p].Colors[0];
                                byte g = netPoints[p].Colors[1];
                                byte b = netPoints[p].Colors[2];
                                byte a = netPoints[p].Colors[3];

                                hwPoints[p].x = (UInt16)x;
                                hwPoints[p].y = (UInt16)y;
                                hwPoints[p].colors = [r, g, b, a, 0, 0];

                            }
                            #region Debug Output
                            /*for(int i = 0; i<hwPoints.Length; i++) {
                                server.sendMessage("INFO: Sending x="+hwPoints[i].x+" y="+hwPoints[i].y+" c={"+hwPoints[i].colors[0]+", "+hwPoints[i].colors[1]+", "+hwPoints[i].colors[2]+", "+hwPoints[i].colors[3]+", "+hwPoints[i].colors[4]+", "+hwPoints[i].colors[5]+"} scanrate="+parcel.DeviceScanrate);
                            }*/
                            #endregion
                            // send laser points to device
                            int code2 = SendFrame(index, hwPoints, (UInt32)hwPoints.Length, (UInt32)parcel.DeviceScanrate);
                            if(code2!=0) {
                                //server.sendMessage("WARNING: Sending laser points to device with index "+parcel.DeviceAddress+" failed: "+getErrorCode(code2));
                                server.sendExceptionCode(parcel.DeviceAddress, code1, getErrorCode(code1)+" [K:"+code1+"]");
                            }
                        } else {
                            server.sendExceptionCode(parcel.DeviceAddress, 5, "OVERFLOW");
                        }
                    } else {
                        //server.sendMessage("WARNING: Could not query laser device with index "+parcel.DeviceAddress+" for overflow state: "+getErrorCode(code1));
                        server.sendExceptionCode(parcel.DeviceAddress, code1, getErrorCode(code1)+" [E:"+code1+"]");
                    }
                } catch(InvalidDeviceException) {
                    server.sendExceptionCode(parcel.DeviceAddress, 3, getErrorCode(3)+" [I:3]");
                }
            }
        }

        private void queryDeviceAddress(LaserController controller) {
            IntPtr pAddress;
            int code = GetDeviceIdentifier((UInt32)controller.Index, out pAddress);
            if(code==0) {
                string result = Marshal.PtrToStringUni(pAddress);
                var matches = Regex.Match(result, @"\b(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\b");
                if(matches.Success) {
                    controller.Address = matches.Captures[0].Value;
                }
            } else {
                if(server!=null) {
                    server.sendMessage("WARNING: Could not query address for laser controller with index: "+controller.Index+" ["+getErrorCode(code)+"]");
                }
                throw new InvalidDeviceException();
            }
        }

        private void queryDeviceProperties(LaserController controller) {
            HwDeviceInfo pDevInfo = new HwDeviceInfo();
            int code = GetDeviceInfo((UInt32)controller.Index, ref pDevInfo);
            if(code==0) {
                controller.MinScanrate = pDevInfo.minScanrate;
                controller.MaxScanrate = pDevInfo.maxScanrate;
                controller.MaxNumOfPoints = pDevInfo.maxNumOfPoints;
                controller.DeviceType = pDevInfo.type;
            } else {
                if(server!=null) {
                    server.sendMessage("WARNING: Could not query device info for laser device with index: "+controller.Index+" ["+getErrorCode(code)+"]");
                }
                throw new InvalidDeviceException();
            }
        }

        private string getErrorCode(int code) {
            switch(code) {
                case 0:
                    return "OK";
                case 1:
                    return "FAILED";
                case 2:
                    return "DLL NOT OPEN";
                case 3:
                    return "INVALID DEVICE (DEVICE INDEX NOT FOUND)";
                case 4:
                    return "FRAME NOT SENT";
                default:
                    return "UNKNOWN ERROR";
            }
        }
        #endregion

        #region Entry Point
        public static void Main(string[] args) {
            Program program = new Program();
            program.launch(int.Parse(args[0]));
        }
        #endregion

    }

    #region Class: Toolkit
    public class Toolkit {

        public static float clamp(float value, float min, float max) {
            if(value<min) {
                return min;
            } else if(value>max) {
                return max;
            } else {
                return value;
            }
        }

        public static void clamp(ref float value, float min, float max) {
            if(value<min) {
                value=min;
            } else if(value>max) {
                value=max;
            }
        }

        public static double clamp(double value, double min, double max) {
            if(value<min) {
                return min;
            } else if(value>max) {
                return max;
            } else {
                return value;
            }
        }

        public static void clamp(ref double value, double min, double max) {
            if(value<min) {
                value = min;
            } else if(value>max) {
                value = max;
            }
        }

    }
    #endregion

}