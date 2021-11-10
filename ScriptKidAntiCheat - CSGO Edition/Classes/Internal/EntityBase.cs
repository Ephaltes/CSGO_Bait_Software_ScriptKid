using System;
using ScriptKidAntiCheat.Data;
using ScriptKidAntiCheat.Utils;
using SharpDX;

/*
 * Credit: https://github.com/rciworks/RCi.Tutorials.Csgo.Cheat.External
 */
namespace ScriptKidAntiCheat.Internal
{
    public abstract class EntityBase
    {
        public IntPtr AddressBase { get; protected set; }

        public bool LifeState { get; protected set; }

        public int Health { get; protected set; }

        public int ClassID { get; protected set; }

        public Team Team { get; protected set; }

        public Vector3 Origin { get; private set; }

        public virtual bool IsAlive()
        {
            return AddressBase != IntPtr.Zero &&
                   !LifeState &&
                   Health > 0 &&
                   (Team == Team.Terrorists || Team == Team.CounterTerrorists);
        }

        public virtual bool IsValid()
        {
            return AddressBase != IntPtr.Zero;
        }

        protected abstract IntPtr ReadAddressBase(GameProcess gameProcess);
        public virtual bool Update(GameProcess gameProcess)
        {
            AddressBase = ReadAddressBase(gameProcess);

            if (AddressBase == IntPtr.Zero)
            {
                return false;
            }

            LifeState = gameProcess.Process.Read<bool>(AddressBase + Offsets.m_lifeState);
            Health = gameProcess.Process.Read<int>(AddressBase + Offsets.m_iHealth);
            Team = (Team)gameProcess.Process.Read<int>(AddressBase + Offsets.m_iTeamNum);
            Origin = gameProcess.Process.Read<Vector3>(AddressBase + Offsets.m_vecOrigin);

            IntPtr one = gameProcess.Process.Read<IntPtr>(AddressBase + 0x8);
            IntPtr two = gameProcess.Process.Read<IntPtr>(one + 0x8);
            IntPtr three = gameProcess.Process.Read<IntPtr>(two + 0x1);
            ClassID = gameProcess.Process.Read<int>(three + 0x14);

            return true;
        }
    }
}
