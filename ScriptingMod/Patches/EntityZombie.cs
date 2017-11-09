using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace ScriptingMod.Patches
{
    public class EntityZombie : EntityEnemy
    {
        [UsedImplicitly]
        protected override Vector3i dropCorpseBlock()
        {
            Log.Warning("Called patched EntityZombie.dropCorpseBlock() ...");

            if (this.lootContainer != null && this.lootContainer.IsUserAccessing())
            {
                return Vector3i.zero;
            }
            Vector3i _pos = base.dropCorpseBlock();
            if (_pos == Vector3i.zero)
            {
                return Vector3i.zero;
            }
            TileEntityLootContainer tileEntity = this.world.GetTileEntity(0, _pos) as TileEntityLootContainer;
            if (tileEntity == null)
            {
                return Vector3i.zero;
            }
            if (this.lootContainer != null)
            {
                // --------------
                // PATCH: Don't copy anything if zombie was opened before, but set the empty container size to the zombie's container size
                //tileEntity.CopyLootContainerDataFromOther(this.lootContainer);
                tileEntity.SetContainerSize(this.lootContainer.GetContainerSize());
                Log.Warning("Prevented possible item duplication.");
                // --------------
            }
            else
            {
                tileEntity.lootListIndex = this.lootListOnDeath;
                tileEntity.SetContainerSize(LootContainer.lootList[this.lootListOnDeath].size, true);
            }
            tileEntity.SetModified();
            return _pos;
        }
    }
}
