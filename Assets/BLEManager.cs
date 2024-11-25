using System;
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

                if (name.Contains("muse_v3"))
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
                                            (notifyAddress, notifyCharacteristic) => { Debug.Log("subscribed to data"); },
                                            (address, characteristicUUID, bytes) => { Debug.Log(BitConverter.ToString(bytes)); });
                                    }, (address, characteristicUUID, bytes) => { Debug.Log(BitConverter.ToString(bytes)); });
                            }
                        });
                }
            }, (address, name, rssi, bytes) => { Debug.Log(name); });
    }
    
    public void SendByte()
    {
        var charUUID = commandUuid;
        
        byte[] buffer = new byte[7];
 
        // Set the first byte: CMD_BLE_NAME (0x0C) + READ_BIT_MASK (0x80)
        
        buffer[0] = 0x02;
        buffer[1] = 0x05;
        buffer[2] = 0x08;
        buffer[3] = 0x10; //quaternion
        buffer[4] = 0x00;
        buffer[5] = 0x00;
        buffer[6] = 0x01;
        

        //buffer[0] = 0x8c;
        //buffer[1] = 0x00;
            
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, _serviceUUID, charUUID, buffer, buffer.Length, true, (characteristicUUID) =>
        {
            BluetoothLEHardwareInterface.Log("Write Succeeded");
        });
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