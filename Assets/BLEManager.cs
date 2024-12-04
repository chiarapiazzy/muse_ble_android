using System;
using System.IO;
using System.Linq;
using UnityEngine;

public class BLEManager : MonoBehaviour
{
    public static BLEManager Instance { get; private set; }

    private readonly string dataUuid = "09bf2c52-d1d9-c0b7-4145-475964544307";
    private readonly string commandUuid = "d5913036-2d8a-41ee-85b9-4e361aa5c8a7";

    public event Action OnDeviceConnected;

    private string _deviceAddress;
    private string _serviceUUID;

    private bool android;

    private float previousX;
    private float previousY;

    public BLEStatus status = BLEStatus.Disconnected;

    public byte[] value = new byte[20];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_ANDROID
        android = true;
#endif

        if (android)
            BluetoothLEHardwareInterface.Initialize(true, false, () => { },
                error =>
                {
                    BluetoothLEHardwareInterface.Log("Error: " + error);

                    if (error.Contains("Bluetooth LE Not Enabled"))
                        BluetoothLEHardwareInterface.BluetoothEnable(true);
                });
    }

    private void Reset()
    {
        status = BLEStatus.Disconnected;
    }

    public void StartScan()
    {
        Debug.Log("Starting scan!");
        Debug.Log("Android is selected? " + android);

        if (android)
            BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, (address, name) =>
            {
                Debug.Log(name);

                if (name.Contains("muse_PDR"))
                {
                    _deviceAddress = address;

                    BluetoothLEHardwareInterface.ConnectToPeripheral(_deviceAddress, null, null,
                        (address, serviceUUID, characteristicUUID) =>
                        {
                            status = BLEStatus.Connected;
                            BluetoothLEHardwareInterface.StopScan();

                            if (characteristicUUID == commandUuid)
                            {
                                _serviceUUID = serviceUUID;

                                BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(_deviceAddress,
                                    _serviceUUID, commandUuid, (notifyAddress, notifyCharacteristic) =>
                                    {
                                        Debug.Log("subscribed to command");
                                        status = BLEStatus.Streaming;

                                        // read the initial state of the button
                                        // BluetoothLEHardwareInterface.ReadCharacteristic(_deviceAddress, _serviceUUID,
                                        //     dataUuid, (characteristic, bytes) => { StoreDataframe(bytes); });
                                        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(
                                            _deviceAddress, _serviceUUID, dataUuid,
                                            (notifyAddress, notifyCharacteristic) =>
                                            {
                                                Debug.Log("subscribed to data");
                                            },
                                            (address, characteristicUUID, bytes) =>
                                            {
                                                //Debug.Log(BitConverter.ToString(bytes));
                                                PrintPDR(bytes);
                                            });
                                    },
                                    (address, characteristicUUID, bytes) =>
                                    {
                                        Debug.Log(BitConverter.ToString(bytes));
                                    });
                            }
                        });
                }
            }, (address, name, rssi, bytes) => { Debug.Log(name); });
    }

    private void PrintPDR(byte[] bytes)
    {
        //Debug.Log("entrato nella PDR = true");
        if (bytes.Length >= 12)
        {
            //Debug.Log("bytes + 12");
            //Debug.Log("bytes ricevuti:" + bytes);
            var
                ultimiDodici =
                    bytes.TakeLast(12)
                        .ToArray(); // Separazione dei segmenti
            var primiDue = ultimiDodici[..2]; // Activity Type
            var secondiDue = ultimiDodici[2..4]; // Step Counter
            var terziQuattro = ultimiDodici[4..8]; // X Position
            var ultimiQuattro = ultimiDodici[8..12]; // Y Position
            // Decodifica dei segmenti
            var activityType = DecodeUInt16(primiDue);
            var stepCounter = DecodeUInt16(secondiDue);
            var xPosition = DecodeFloat(terziQuattro);
            var yPosition = DecodeFloat(ultimiQuattro);

            // Utilizzo dei valori decodificati
            Debug.Log($"Activity Type: {activityType}");
            Debug.Log($"Step Counter: {stepCounter}");
            Debug.Log($"X Position: {xPosition}");
            Debug.Log($"Y Position: {yPosition}");

            var deltaX = previousX - xPosition;
            var deltaY = previousY - yPosition;
            var length = Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (length > 0.05f)
            {
                Debug.Log("Step length: " + Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY));
            }
            
            var filePath = Path.Combine(Application.persistentDataPath, "data.csv");
            string[] data = { xPosition + "," + yPosition };
            using (var writer = new StreamWriter(filePath, true))
            {
                foreach (var line in data) writer.WriteLine(line);
            }

            previousX = xPosition;
            previousY = yPosition;
        }
    }

    public static ushort DecodeUInt16(byte[] data)
    {
        if (data.Length != 2) throw new ArgumentException("Il segmento deve essere lungo 2 byte.");
        if (BitConverter.IsLittleEndian)
            return BitConverter.ToUInt16(data, 0);
        // Inverti i byte per Little Endian
        return BitConverter.ToUInt16(data.Reverse().ToArray(), 0);
    }

    // Funzione per decodificare un array di 4 byte in un float
    public static float DecodeFloat(byte[] data)
    {
        if (data.Length != 4)
            throw new ArgumentException("Il segmento deve essere lungo 4 byte.");
        if (BitConverter.IsLittleEndian)
            return BitConverter.ToSingle(data, 0);
        // Inverti i byte per Little Endian
        return BitConverter.ToSingle(data.Reverse().ToArray(), 0);
    }


    public void SendByte()
    {
        var charUUID = commandUuid;

        var buffer = new byte[7];

        // Set the first byte: CMD_BLE_NAME (0x0C) + READ_BIT_MASK (0x80)

        buffer[0] = 0x02;
        buffer[1] = 0x05;
        buffer[2] = 0x08;
        buffer[3] = 0x00; //pdr
        buffer[4] = 0x00;
        buffer[5] = 0x20;
        buffer[6] = 0x04;


        //buffer[0] = 0x8c;
        //buffer[1] = 0x00;

        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, _serviceUUID, charUUID, buffer,
            buffer.Length, true, characteristicUUID => { BluetoothLEHardwareInterface.Log("Write Succeeded"); });
    }

    public void SetIDLE()
    {
        var charUUID = commandUuid;
        var buffer = new byte[3];
        // Set the first byte: CMD_BLE_NAME (0x0C) + READ_BIT_MASK (0x80)
        buffer[0] = 0x02;
        buffer[1] = 0x01;
        buffer[2] = 0x02;
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, _serviceUUID, charUUID, buffer, buffer.Length,
            true, characteristicUUID => { BluetoothLEHardwareInterface.Log("Write Succeeded"); });
    }

    private void StoreDataframe(byte[] value)
    {
    }

    private void OnApplicationQuit()
    {
    }

    // Clean up when the object is destroyed (for example, when exiting play mode)
    private void OnDestroy()
    {
    }
}