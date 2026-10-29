using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Pure-logic regression checks for placing the Pet Cafe (kind 3) and Pet Hospital (kind 4).
// Run from the editor: PetTownPlacementRegressionQA.Run() returns a report; "FAIL" lines mean a regression.
public static class PetTownPlacementRegressionQA
{
    static int passed, failed;

    static PetTownBuildingSaveRecord Record(int kind, int facing)
    {
        var def = PetTownBuildingDefinition.Default(kind);
        return new PetTownBuildingSaveRecord
        {
            id = "qa-" + kind, kind = kind, facingQuarter = facing, groundY = 0, footprintMeters = def.footprintMeters,
            entranceLocal = def.entranceLocal, hasEntrance = def.hasEntrance, modelHeight = 3.8f
        };
    }

    static PetTownPlacementSafetyContext Flat()
    {
        return new PetTownPlacementSafetyContext { WorldHalfSize = 110, Height = (x, z) => 0f };
    }

    static PetTownPlacementSafetySettings Settings()
    {
        return new PetTownPlacementSafetySettings { checkNavigation = false };
    }

    static PetTownPlacementObstacle Block(Rect footprint)
    {
        return new PetTownPlacementObstacle { Footprint = footprint, GroundY = 0 };
    }

    static void Check(StringBuilder log, string name, bool condition, string detail = "")
    {
        if (condition) { passed++; log.AppendLine("PASS " + name); }
        else { failed++; log.AppendLine("FAIL " + name + " " + detail); }
    }

    static void Expect(StringBuilder log, string name, PetTownPlacementValidation result, bool canPlace, string code = null)
    {
        bool ok = result.CanPlace == canPlace && (code == null || result.Code == code);
        Check(log, name, ok, "(CanPlace=" + result.CanPlace + " Code=" + result.Code + ")");
    }

    public static string Run()
    {
        passed = failed = 0;
        var log = new StringBuilder();
        foreach (int kind in new[] { 3, 4 })
        {
            string label = kind == 3 ? "Cafe" : "Clinic";
            var record = Record(kind, 2);
            Check(log, label + ".recordValid", record.ValidFor(kind));
            Vector3 origin = Vector3.zero;

            Expect(log, label + ".openGroundAllowed", PetTownPlacementValidator.Validate(origin, record, Settings(), Flat()), true);

            for (int facing = 0; facing < 4; facing++)
                Expect(log, label + ".facing" + facing, PetTownPlacementValidator.Validate(origin, Record(kind, facing), Settings(), Flat()), true);

            var water = Flat(); water.Water = (x, z) => true;
            Expect(log, label + ".waterBlocked", PetTownPlacementValidator.Validate(origin, record, Settings(), water), false, "Water");

            Expect(log, label + ".outOfBoundsBlocked", PetTownPlacementValidator.Validate(new Vector3(108, 0, 0), record, Settings(), Flat()), false, "Bounds");

            var overlap = Flat(); overlap.Buildings.Add(Block(PetTownPlacementValidator.Footprint(origin, record.WorldFootprint)));
            Expect(log, label + ".overlapBlocked", PetTownPlacementValidator.Validate(origin, record, Settings(), overlap), false, "Building");

            var tight = Flat();
            var body = PetTownPlacementValidator.Footprint(origin, record.WorldFootprint);
            tight.Buildings.Add(Block(Rect.MinMaxRect(body.xMax + .3f, body.yMin, body.xMax + 6f, body.yMax)));
            var tightResult = PetTownPlacementValidator.Validate(origin, record, Settings(), tight);
            Check(log, label + ".tooCloseNeighbourBlocked", !tightResult.CanPlace, "(Code=" + tightResult.Code + ")");

            var far = Flat();
            far.Buildings.Add(Block(PetTownPlacementValidator.Footprint(new Vector3(40, 0, 0), new Vector2(15, 15))));
            Expect(log, label + ".farNeighbourAllowed", PetTownPlacementValidator.Validate(origin, record, Settings(), far), true);

            var inFront = Flat();
            Vector3 entrance = origin + record.Rotation * record.entranceLocal;
            Vector3 ahead = entrance + record.EntranceForward * 1.2f;
            inFront.Buildings.Add(Block(Rect.MinMaxRect(ahead.x - 3f, ahead.z - 3f, ahead.x + 3f, ahead.z + 3f)));
            var frontResult = PetTownPlacementValidator.Validate(origin, record, Settings(), inFront);
            Check(log, label + ".blockedEntranceRejected", !frontResult.CanPlace, "(Code=" + frontResult.Code + ")");

            var actor = Flat(); actor.Actors.Add(new Vector3(0, 0, 0));
            Expect(log, label + ".actorInsideBlocked", PetTownPlacementValidator.Validate(origin, record, Settings(), actor), false, "Actor");

            var steep = Flat(); steep.Height = (x, z) => x * 2f;
            Check(log, label + ".steepSlopeRejected", !PetTownPlacementValidator.Validate(origin, record, Settings(), steep).CanPlace);

            Check(log, label + ".earthworkLargerThanBody", PetTownPlacementValidator.EarthworkSize(record).x > record.WorldFootprint.x);
        }
        log.AppendLine("RESULT passed=" + passed + " failed=" + failed);
        return log.ToString();
    }
}
