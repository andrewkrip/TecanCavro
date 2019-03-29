using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO.Ports;
using SerialPortExtension.SendReceive;

namespace TecanCavroControl
{
    public class TecanCavro
    {
        private SerialPort serialPort;
        private readonly int address;
        public bool isConnected { get; private set; } = false;
        public enum ValvePosition : int { pos1 = 1, pos2, pos3 };
        public enum ErrorCode : int
        {
            NoError = 0, Initialization = 1, InvalidCommand = 2, InvalidOperand = 3,
            InvalidCommandSequence = 4, Unused = 5, EEPROMFailure = 6, DeviceNotInitialized = 7,
            PlungerOverload = 9, ValveOverload = 10, PlungerMoveNotAllowed = 11, CommandOverflow = 15
        };

        public TecanCavro(int address = 1)
        {
            this.address = address;
        }

        #region Public Control Methods
        public void Connect()
        {
            if (isConnected)
                return;
            foreach (string port in SerialPort.GetPortNames())
            {
                serialPort = new SerialPort(port) { NewLine = "\x03\r\n" };
                try
                {
                    serialPort.Open();
                    serialPort.ReadTimeout = 100;
                    serialPort.DiscardInBuffer();
                    serialPort.DiscardOutBuffer();
                    Response response = SendReceive("Q");
                    isConnected = true;
                    return;
                }
                catch { serialPort.Close(); }
            }
            throw new Exception("Cavro Not Found");
        }

        public void Disconnect()
        {
            isConnected = false;
            serialPort?.Close();
            serialPort?.Dispose();
        }
        
        public void Initialize()
        {
            ErrorCode status;
            do
            {
                WaitForReady();
                status = SendReceive("Z0,0,0R").status;
            } while (status == ErrorCode.DeviceNotInitialized);
        }

        public void SetSpeed(ushort speed)
        {
            WaitForReady();
            if (speed > 40) { speed = 40; }
            Response response = SendReceive("S" + speed.ToString() + "R");
            CheckResponse(response);
        }

        public void SetValvePosition(ValvePosition valvePosition)
        {
            WaitForReady();
            Response response = SendReceive("I" + valvePosition.ToString() + "R");
            CheckResponse(response);
        }

        public void SetAbsolutePosition(ushort position)
        {
            WaitForReady();
            if (position > 3000) { position = 3000; }
            Response response = SendReceive("A" + position.ToString() + "R");
            CheckResponse(response);
        }
        #endregion

        private void WaitForReady()
        {
            bool isReady = SendReceive("Q").isReady;
            while (!isReady)
            {
                Thread.Sleep(500);
                isReady = SendReceive("Q").isReady;
            }
        }

        private void CheckResponse(Response response)
        {
            if (response.status != ErrorCode.NoError)
                throw new Exception(response.status.ToString());
        }

        private Response SendReceive(string request)
        {
            string formattedRequest = "/" + address.ToString() + request + "\r";
            return new Response(serialPort.SendReceive(formattedRequest));
        }

        private class Response
        {
            public readonly ErrorCode status;
            public readonly string[] data;
            public readonly string hexString;
            public readonly bool isReady;

            public Response(string stringResponse)
            {
                byte[] byteArray = Encoding.Default.GetBytes(stringResponse);
                hexString = BitConverter.ToString(byteArray);
                string[] hexCharacters = hexString.Split('-');
                status = GetStatus(hexCharacters[2]);
                isReady = hexCharacters[2][0] == '6';
                int dataLength = hexCharacters.Length - 3;
                if (dataLength > 0)
                    data = hexCharacters.Skip(3).Take(dataLength).ToArray();
                else
                    data = new string[] { };
            }

            private static ErrorCode GetStatus(string hexCharacter)
            {
                ErrorCode errorCode = ErrorCode.Unused;
                switch (hexCharacter)
                {
                    case "40":
                    case "60":
                        errorCode = ErrorCode.NoError;
                        break;
                    case "41":
                    case "61":
                        errorCode = ErrorCode.Initialization;
                        break;
                    case "42":
                    case "62":
                        errorCode = ErrorCode.InvalidCommand;
                        break;
                    case "43":
                    case "63":
                        errorCode = ErrorCode.InvalidOperand;
                        break;
                    case "44":
                    case "64":
                        errorCode = ErrorCode.InvalidCommandSequence;
                        break;
                    case "46":
                    case "66":
                        errorCode = ErrorCode.EEPROMFailure;
                        break;
                    case "47":
                    case "67":
                        errorCode = ErrorCode.DeviceNotInitialized;
                        break;
                    case "49":
                    case "69":
                        errorCode = ErrorCode.PlungerOverload;
                        break;
                    case "4A":
                    case "6A":
                        errorCode = ErrorCode.ValveOverload;
                        break;
                    case "4B":
                    case "6B":
                        errorCode = ErrorCode.PlungerMoveNotAllowed;
                        break;
                    case "4F":
                    case "6F":
                        errorCode = ErrorCode.CommandOverflow;
                        break;
                }
                return errorCode;
            }
        }
    }
}