﻿// Generated by .NET Reflector from C:\Projects\Skylines\DifficultyMod\DifficultyMod\libs\Assembly-CSharp.dll
using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class BusAIMod : CarAIMod
{
    public int m_passengerCapacity = 30;
    public int m_ticketPrice = 100;
    public TransportInfo m_transportInfo;

    public override bool ArriveAtDestination(ushort vehicleID, ref Vehicle vehicleData)
    {
        if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) != Vehicle.Flags.None)
        {
            return this.ArriveAtSource(vehicleID, ref vehicleData);
        }
        return this.ArriveAtTarget(vehicleID, ref vehicleData);
    }

    private bool ArriveAtSource(ushort vehicleID, ref Vehicle data)
    {
        if (data.m_sourceBuilding == 0)
        {
            Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
            return true;
        }
        this.RemoveSource(vehicleID, ref data);
        Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
        return true;
    }

    private bool ArriveAtTarget(ushort vehicleID, ref Vehicle data)
    {
        if (data.m_targetBuilding == 0)
        {
            Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
            return true;
        }
        ushort nextStop = 0;
        if (data.m_transportLine != 0)
        {
            nextStop = TransportLine.GetNextStop(data.m_targetBuilding);
        }
        ushort targetBuilding = data.m_targetBuilding;
        this.UnloadPassengers(vehicleID, ref data, targetBuilding, nextStop);
        if (nextStop == 0)
        {
            data.m_flags |= Vehicle.Flags.GoingBack;
            if (!this.StartPathFind(vehicleID, ref data))
            {
                return true;
            }
            data.m_flags &= ~Vehicle.Flags.Arriving;
            data.m_flags |= Vehicle.Flags.Stopped;
            data.m_waitCounter = 0;
        }
        else
        {
            data.m_targetBuilding = nextStop;
            if (!this.StartPathFind(vehicleID, ref data))
            {
                return true;
            }
            this.LoadPassengers(vehicleID, ref data, targetBuilding, nextStop);
            data.m_flags &= ~Vehicle.Flags.Arriving;
            data.m_flags |= Vehicle.Flags.Stopped;
            data.m_waitCounter = 0;
        }
        return false;
    }

    protected override void CalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed)
    {
        if ((vehicleData.m_flags & (Vehicle.Flags.Arriving | Vehicle.Flags.Leaving)) != Vehicle.Flags.None)
        {
            NetManager instance = Singleton<NetManager>.instance;
            NetInfo info = instance.m_segments.m_buffer[position.m_segment].Info;
            if ((info.m_lanes != null) && (info.m_lanes.Length > position.m_lane))
            {
                NetInfo.Lane lane = info.m_lanes[position.m_lane];
                float stopOffset = lane.m_stopOffset;
                if ((instance.m_segments.m_buffer[position.m_segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                {
                    stopOffset = -stopOffset;
                }
                instance.m_lanes.m_buffer[laneID].CalculateStopPositionAndDirection(offset * 0.003921569f, stopOffset, out pos, out dir);
                maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, info.m_lanes[position.m_lane].m_speedLimit, instance.m_lanes.m_buffer[laneID].m_curve);
                return;
            }
        }
        base.CalculateSegmentPosition(vehicleID, ref vehicleData, position, laneID, offset, out pos, out dir, out maxSpeed);
    }

    public override bool CanLeave(ushort vehicleID, ref Vehicle vehicleData)
    {
        if (vehicleData.m_waitCounter < 12)
        {
            return false;
        }
        return base.CanLeave(vehicleID, ref vehicleData);
    }

    public override void CreateVehicle(ushort vehicleID, ref Vehicle data)
    {
        base.CreateVehicle(vehicleID, ref data);
        Singleton<CitizenManager>.instance.CreateUnits(out data.m_citizenUnits, ref Singleton<SimulationManager>.instance.m_randomizer, 0, vehicleID, 0, 0, 0, this.m_passengerCapacity, 0);
    }

    public override void GetBufferStatus(ushort vehicleID, ref Vehicle data, out string localeKey, out int current, out int max)
    {
        localeKey = "Default";
        current = data.m_transferSize;
        max = this.m_passengerCapacity;
    }

    public override Color GetColor(ushort vehicleID, ref Vehicle data, InfoManager.InfoMode infoMode)
    {
        if ((infoMode != InfoManager.InfoMode.Transport) && (infoMode != InfoManager.InfoMode.None))
        {
            return base.GetColor(vehicleID, ref data, infoMode);
        }
        ushort transportLine = data.m_transportLine;
        if (transportLine != 0)
        {
            return Singleton<TransportManager>.instance.m_lines.m_buffer[transportLine].GetColor();
        }
        return Singleton<TransportManager>.instance.m_properties.m_transportColors[(int)this.m_transportInfo.m_transportType];
    }

    public override string GetLocalizedStatus(ushort vehicleID, ref Vehicle data, out InstanceID target)
    {
        if ((data.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None)
        {
            target = InstanceID.Empty;
            return Locale.Get("VEHICLE_STATUS_BUS_STOPPED");
        }
        if ((data.m_flags & Vehicle.Flags.GoingBack) != Vehicle.Flags.None)
        {
            target = InstanceID.Empty;
            return Locale.Get("VEHICLE_STATUS_BUS_RETURN");
        }
        if (data.m_transportLine != 0)
        {
            target = InstanceID.Empty;
            return Locale.Get("VEHICLE_STATUS_BUS_ROUTE");
        }
        target = InstanceID.Empty;
        return Locale.Get("VEHICLE_STATUS_CONFUSED");
    }

    public override string GetLocalizedStatus(ushort parkedVehicleID, ref VehicleParked data, out InstanceID target)
    {
        target = InstanceID.Empty;
        return Locale.Get("VEHICLE_STATUS_BUS_STOPPED");
    }

    public override InstanceID GetOwnerID(ushort vehicleID, ref Vehicle vehicleData)
    {
        return new InstanceID();
    }

    public static float GetPathProgress(uint path, int pathPos, float min, float max, out bool valid)
    {
        valid = false;
        if (pathPos == 0xff)
        {
            pathPos = 0;
        }
        else
        {
            pathPos = pathPos >> 1;
        }
        PathManager instance = Singleton<PathManager>.instance;
        if ((instance.m_pathUnits.m_buffer[path].m_pathFindFlags & 4) == 0)
        {
            return min;
        }
        float length = instance.m_pathUnits.m_buffer[path].m_length;
        uint nextPathUnit = instance.m_pathUnits.m_buffer[path].m_nextPathUnit;
        float num3 = 0f;
        if (nextPathUnit != 0)
        {
            num3 = instance.m_pathUnits.m_buffer[nextPathUnit].m_length;
        }
        int positionCount = instance.m_pathUnits.m_buffer[path].m_positionCount;
        if (positionCount == 0)
        {
            return min;
        }
        float num5 = length + (((num3 - length) * pathPos) / ((float)positionCount));
        valid = true;
        return Mathf.Clamp(max - num5, min, max);
    }

    public override bool GetProgressStatus(ushort vehicleID, ref Vehicle data, out float current, out float max)
    {
        ushort transportLine = data.m_transportLine;
        ushort targetBuilding = data.m_targetBuilding;
        if ((transportLine != 0) && (targetBuilding != 0))
        {
            bool flag;
            float num3;
            float num4;
            float num5;
            Singleton<TransportManager>.instance.m_lines.m_buffer[transportLine].GetStopProgress(targetBuilding, out num3, out num4, out num5);
            uint path = data.m_path;
            if ((path == 0) || ((data.m_flags & Vehicle.Flags.WaitingPath) != Vehicle.Flags.None))
            {
                current = num3;
                flag = false;
            }
            else
            {
                current = GetPathProgress(path, data.m_pathPositionIndex, num3, num4, out flag);
            }
            max = num5;
            return flag;
        }
        current = 0f;
        max = 0f;
        return true;
    }

    public override void GetSize(ushort vehicleID, ref Vehicle data, out int size, out int max)
    {
        size = 0;
        max = this.m_passengerCapacity;
    }

    public override InstanceID GetTargetID(ushort vehicleID, ref Vehicle vehicleData)
    {
        InstanceID eid = new InstanceID();
        if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) != Vehicle.Flags.None)
        {
            eid.Building = vehicleData.m_sourceBuilding;
            return eid;
        }
        eid.NetNode = vehicleData.m_targetBuilding;
        return eid;
    }

    public override int GetTicketPrice(ushort vehicleID, ref Vehicle vehicleData)
    {
        DistrictManager instance = Singleton<DistrictManager>.instance;
        byte district = instance.GetDistrict((Vector3)vehicleData.m_targetPos3);
        if ((instance.m_districts.m_buffer[district].m_servicePolicies & DistrictPolicies.Services.FreeTransport) == DistrictPolicies.Services.None)
        {
            return this.m_ticketPrice;
        }
        instance.m_districts.m_buffer[district].m_servicePoliciesEffect |= DistrictPolicies.Services.FreeTransport;
        return 0;
    }

    private void LoadPassengers(ushort vehicleID, ref Vehicle data, ushort currentStop, ushort nextStop)
    {
        if ((currentStop != 0) && (nextStop != 0))
        {
            CitizenManager instance = Singleton<CitizenManager>.instance;
            NetManager manager2 = Singleton<NetManager>.instance;
            Vector3 position = manager2.m_nodes.m_buffer[currentStop].m_position;
            Vector3 nextTarget = manager2.m_nodes.m_buffer[nextStop].m_position;
            manager2.m_nodes.m_buffer[currentStop].m_maxWaitTime = 0;
            int num = Mathf.Max((int)(((position.x - 32f) / 8f) + 1080f), 0);
            int num2 = Mathf.Max((int)(((position.z - 32f) / 8f) + 1080f), 0);
            int num3 = Mathf.Min((int)(((position.x + 32f) / 8f) + 1080f), 0x86f);
            int num4 = Mathf.Min((int)(((position.z + 32f) / 8f) + 1080f), 0x86f);
            int tempCounter = manager2.m_nodes.m_buffer[currentStop].m_tempCounter;
            int transferSize = data.m_transferSize;
            bool flag = false;
            for (int i = num2; (i <= num4) && !flag; i++)
            {
                for (int j = num; (j <= num3) && !flag; j++)
                {
                    ushort index = instance.m_citizenGrid[(i * 0x870) + j];
                    int num10 = 0;
                    while ((index != 0) && !flag)
                    {
                        ushort nextGridInstance = instance.m_instances.m_buffer[index].m_nextGridInstance;
                        if (((instance.m_instances.m_buffer[index].m_flags & CitizenInstance.Flags.WaitingTransport) != CitizenInstance.Flags.None) && (Vector3.SqrMagnitude(((Vector3)instance.m_instances.m_buffer[index].m_targetPos) - position) < 1024f))
                        {
                            CitizenInfo info = instance.m_instances.m_buffer[index].Info;
                            if (info.m_citizenAI.TransportArriveAtSource(index, ref instance.m_instances.m_buffer[index], position, nextTarget))
                            {
                                if (info.m_citizenAI.SetCurrentVehicle(index, ref instance.m_instances.m_buffer[index], vehicleID, 0, position))
                                {
                                    tempCounter++;
                                    transferSize++;
                                }
                                else
                                {
                                    flag = true;
                                }
                            }
                        }
                        index = nextGridInstance;
                        if (++num10 > 0x10000)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + System.Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            manager2.m_nodes.m_buffer[currentStop].m_tempCounter = (ushort)Mathf.Min(tempCounter, 0xffff);
            data.m_transferSize = (ushort)transferSize;
        }
    }

    public override void LoadVehicle(ushort vehicleID, ref Vehicle data)
    {
        base.LoadVehicle(vehicleID, ref data);
        if (data.m_sourceBuilding != 0)
        {
            Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_sourceBuilding].AddOwnVehicle(vehicleID, ref data);
        }
        if (data.m_transportLine != 0)
        {
            Singleton<TransportManager>.instance.m_lines.m_buffer[data.m_transportLine].AddVehicle(vehicleID, ref data, false);
        }
    }

    public override void ReleaseVehicle(ushort vehicleID, ref Vehicle data)
    {
        this.RemoveSource(vehicleID, ref data);
        this.RemoveLine(vehicleID, ref data);
        base.ReleaseVehicle(vehicleID, ref data);
    }

    private void RemoveLine(ushort vehicleID, ref Vehicle data)
    {
        if (data.m_transportLine != 0)
        {
            Singleton<TransportManager>.instance.m_lines.m_buffer[data.m_transportLine].RemoveVehicle(vehicleID, ref data);
            data.m_transportLine = 0;
        }
    }

    private void RemoveSource(ushort vehicleID, ref Vehicle data)
    {
        if (data.m_sourceBuilding != 0)
        {
            Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_sourceBuilding].RemoveOwnVehicle(vehicleID, ref data);
            data.m_sourceBuilding = 0;
        }
    }

    public override void SetSource(ushort vehicleID, ref Vehicle data, ushort sourceBuilding)
    {
        this.RemoveSource(vehicleID, ref data);
        data.m_sourceBuilding = sourceBuilding;
        if (sourceBuilding != 0)
        {
            Vector3 vector;
            Vector3 vector2;
            data.Unspawn(vehicleID);
            BuildingManager instance = Singleton<BuildingManager>.instance;
            instance.m_buildings.m_buffer[sourceBuilding].Info.m_buildingAI.CalculateSpawnPosition(sourceBuilding, ref instance.m_buildings.m_buffer[sourceBuilding], ref Singleton<SimulationManager>.instance.m_randomizer, base.m_info, out vector, out vector2);
            Quaternion identity = Quaternion.identity;
            Vector3 forward = vector2 - vector;
            if (forward.sqrMagnitude > 0.01f)
            {
                identity = Quaternion.LookRotation(forward);
            }
            data.m_frame0 = new Vehicle.Frame(vector, identity);
            data.m_frame1 = data.m_frame0;
            data.m_frame2 = data.m_frame0;
            data.m_frame3 = data.m_frame0;
            data.m_targetPos0 = vector2;
            data.m_targetPos0.w = 2f;
            data.m_targetPos1 = data.m_targetPos0;
            data.m_targetPos2 = data.m_targetPos0;
            data.m_targetPos3 = data.m_targetPos0;
            this.FrameDataUpdated(vehicleID, ref data, ref data.m_frame0);
            instance.m_buildings.m_buffer[sourceBuilding].AddOwnVehicle(vehicleID, ref data);
        }
    }

    public override void SetTarget(ushort vehicleID, ref Vehicle data, ushort targetBuilding)
    {
        data.m_targetBuilding = targetBuilding;
        if (!this.StartPathFind(vehicleID, ref data))
        {
            data.Unspawn(vehicleID);
        }
    }

    public override void SetTransportLine(ushort vehicleID, ref Vehicle data, ushort transportLine)
    {
        this.RemoveLine(vehicleID, ref data);
        data.m_transportLine = transportLine;
        if (transportLine != 0)
        {
            Singleton<TransportManager>.instance.m_lines.m_buffer[transportLine].AddVehicle(vehicleID, ref data, true);
        }
        else
        {
            data.m_flags |= Vehicle.Flags.GoingBack;
        }
        if (!this.StartPathFind(vehicleID, ref data))
        {
            data.Unspawn(vehicleID);
        }
    }

    private bool ShouldReturnToSource(ushort vehicleID, ref Vehicle data)
    {
        if (data.m_sourceBuilding != 0)
        {
            BuildingManager instance = Singleton<BuildingManager>.instance;
            if (((instance.m_buildings.m_buffer[data.m_sourceBuilding].m_flags & Building.Flags.Active) == Building.Flags.None) && (instance.m_buildings.m_buffer[data.m_sourceBuilding].m_fireIntensity == 0))
            {
                return true;
            }
        }
        return false;
    }

    public override void SimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
    {
        if ((vehicleData.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None)
        {
            vehicleData.m_waitCounter = (byte)(vehicleData.m_waitCounter + 1);
            if (this.CanLeave(vehicleID, ref vehicleData))
            {
                vehicleData.m_flags &= ~Vehicle.Flags.Stopped;
                vehicleData.m_flags |= Vehicle.Flags.Leaving;
                vehicleData.m_waitCounter = 0;
            }
        }
        base.SimulationStep(vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
        if (((vehicleData.m_flags & Vehicle.Flags.GoingBack) == Vehicle.Flags.None) && this.ShouldReturnToSource(vehicleID, ref vehicleData))
        {
            this.SetTransportLine(vehicleID, ref vehicleData, 0);
        }
    }

    protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData)
    {
        if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) != Vehicle.Flags.None)
        {
            if (vehicleData.m_sourceBuilding != 0)
            {
                Vector3 endPos = Singleton<BuildingManager>.instance.m_buildings.m_buffer[vehicleData.m_sourceBuilding].CalculateSidewalkPosition();
                return this.StartPathFind(vehicleID, ref vehicleData, (Vector3)vehicleData.m_targetPos3, endPos);
            }
        }
        else if (vehicleData.m_targetBuilding != 0)
        {
            Vector3 position = Singleton<NetManager>.instance.m_nodes.m_buffer[vehicleData.m_targetBuilding].m_position;
            return this.StartPathFind(vehicleID, ref vehicleData, (Vector3)vehicleData.m_targetPos3, position);
        }
        return false;
    }

    public override void StartTransfer(ushort vehicleID, ref Vehicle data, TransferManager.TransferReason reason, TransferManager.TransferOffer offer)
    {
        if (reason == ((TransferManager.TransferReason)data.m_transferType))
        {
            ushort transportLine = offer.TransportLine;
            this.SetTransportLine(vehicleID, ref data, transportLine);
        }
        else
        {
            base.StartTransfer(vehicleID, ref data, reason, offer);
        }
    }

    public static void TransportArriveAtTarget(ushort vehicleID, ref Vehicle data, Vector3 currentPos, Vector3 targetPos, ref int serviceCounter, ref TransportPassengerData passengerData, bool forceUnload)
    {
        CitizenManager instance = Singleton<CitizenManager>.instance;
        int num = 0;
        uint citizenUnits = data.m_citizenUnits;
        int num3 = 0;
        while (citizenUnits != 0)
        {
            uint nextUnit = instance.m_units.m_buffer[citizenUnits].m_nextUnit;
            for (int i = 0; i < 5; i++)
            {
                uint citizen = instance.m_units.m_buffer[citizenUnits].GetCitizen(i);
                if (citizen != 0)
                {
                    ushort index = instance.m_citizens.m_buffer[citizen].m_instance;
                    if (index != 0)
                    {
                        CitizenInfo info = instance.m_instances.m_buffer[index].Info;
                        if (info.m_citizenAI.TransportArriveAtTarget(index, ref instance.m_instances.m_buffer[index], currentPos, targetPos, ref passengerData, forceUnload))
                        {
                            if (info.m_citizenAI.SetCurrentVehicle(index, ref instance.m_instances.m_buffer[index], 0, 0, currentPos))
                            {
                                serviceCounter++;
                            }
                        }
                        else
                        {
                            num++;
                        }
                    }
                }
            }
            citizenUnits = nextUnit;
            if (++num3 > 0x80000)
            {
                CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + System.Environment.StackTrace);
                break;
            }
        }
        data.m_transferSize = (ushort)num;
    }

    private void UnloadPassengers(ushort vehicleID, ref Vehicle data, ushort currentStop, ushort nextStop)
    {
        if (currentStop != 0)
        {
            NetManager instance = Singleton<NetManager>.instance;
            TransportManager manager2 = Singleton<TransportManager>.instance;
            Vector3 position = instance.m_nodes.m_buffer[currentStop].m_position;
            Vector3 zero = Vector3.zero;
            if (nextStop != 0)
            {
                zero = instance.m_nodes.m_buffer[nextStop].m_position;
            }
            int tempCounter = instance.m_nodes.m_buffer[currentStop].m_tempCounter;
            if (data.m_transportLine != 0)
            {
                TransportArriveAtTarget(vehicleID, ref data, position, zero, ref tempCounter, ref manager2.m_lines.m_buffer[data.m_transportLine].m_passengers, nextStop == 0);
            }
            else
            {
                TransportArriveAtTarget(vehicleID, ref data, position, zero, ref tempCounter, ref manager2.m_passengers[(int)this.m_transportInfo.m_transportType], nextStop == 0);
            }
            instance.m_nodes.m_buffer[currentStop].m_tempCounter = (ushort)Mathf.Min(tempCounter, 0xffff);
        }
    }

    public override void UpdateBuildingTargetPositions(ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos, ushort leaderID, ref Vehicle leaderData, ref int index, float minSqrDistance)
    {
        if (((leaderData.m_flags & Vehicle.Flags.GoingBack) != Vehicle.Flags.None) && (leaderData.m_sourceBuilding != 0))
        {
            Vector3 vector;
            Vector3 vector2;
            BuildingManager instance = Singleton<BuildingManager>.instance;
            BuildingInfo info = instance.m_buildings.m_buffer[leaderData.m_sourceBuilding].Info;
            Randomizer randomizer = new Randomizer((int)vehicleID);
            info.m_buildingAI.CalculateUnspawnPosition(vehicleData.m_sourceBuilding, ref instance.m_buildings.m_buffer[leaderData.m_sourceBuilding], ref randomizer, base.m_info, out vector, out vector2);
            vehicleData.SetTargetPos(index++, base.CalculateTargetPoint(refPos, vector2, minSqrDistance, 2f));
        }
    }
}