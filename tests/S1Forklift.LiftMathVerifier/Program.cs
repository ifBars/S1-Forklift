using S1Forklift;

static void Expect(float expected, float actual, string scenario)
{
    if (Math.Abs(expected - actual) > 0.0001f)
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}.");
}

Expect(1.5f, LiftMath.Advance(1f, 1f, 0.5f, 1f, 0f, 3f), "raises by speed and time");
Expect(3f, LiftMath.Advance(2.9f, 1f, 1f, 1f, 0f, 3f), "clamps at maximum");
Expect(0f, LiftMath.Advance(0.1f, -1f, 1f, 1f, 0f, 3f), "clamps at minimum");
Expect(2f, LiftMath.Advance(2f, 0f, 1f, 1f, 0f, 3f), "holds without input");
Expect(3f, LiftMath.Advance(4f, 0f, 1f, 1f, 0f, 3f), "clamps stale state");

var threw = false;
try
{
    LiftMath.Advance(0f, 1f, 1f, 1f, 2f, 1f);
}
catch (ArgumentOutOfRangeException)
{
    threw = true;
}

if (!threw)
    throw new InvalidOperationException("Invalid bounds must throw.");

Expect(3560f, Constants.ForkliftMassKg, "forklift operating mass");
Expect(8500f, Constants.ForkliftMaximumDrawbarPullNewtons, "forklift drawbar pull");
Expect(30000f, Constants.ForkliftMaximumDrivePowerWatts, "forklift drive power");
Expect(0.265f, Constants.ForkliftDriveWheelRadiusMeters, "forklift drive wheel radius");
Expect(7f, Constants.ForkliftDifferentialGearing, "forklift differential gearing");
Expect(16.5f, Constants.ForkliftGovernorStartSpeedKmh, "forklift governor start speed");
Expect(18f, Constants.ForkliftTopSpeed, "forklift top speed");
Expect(0.18f, Constants.ForkliftBrakeForceMultiplier, "forklift brake force multiplier");
Expect(900f, Constants.ForkliftHandBrakeForce, "forklift handbrake force");
Expect(0.04f, Constants.ForkliftLinearDrag, "forklift linear drag");
Expect(70000f, Constants.ForkliftWheelSpring, "forklift wheel spring");
Expect(14000f, Constants.ForkliftWheelSpringDamper, "forklift wheel damping");
Expect(300f, DrivetrainMath.ScaleTorque(200f, 1.5f), "motor torque scaling");
Expect(0f, DrivetrainMath.ScaleTorque(200f, 0f), "zero motor torque cutoff");
threw = false;
try
{
    DrivetrainMath.ScaleTorque(200f, -0.1f);
}
catch (ArgumentOutOfRangeException)
{
    threw = true;
}

if (!threw)
    throw new InvalidOperationException("Negative motor torque multipliers must throw.");
Expect(
    8500f * 0.265f / 7f,
    DrivetrainMath.CalculateMotorCurveTorque(0f, 8500f, 30000f, 0.265f, 7f, 16.5f, 18f),
    "maximum drawbar pull at launch");
Expect(
    8500f * 0.265f / 7f,
    DrivetrainMath.CalculateMotorCurveTorque(10f, 8500f, 30000f, 0.265f, 7f, 16.5f, 18f),
    "drawbar-limited torque at road-hill speed");
Expect(
    (30000f / (15f / 3.6f)) * 0.265f / 7f,
    DrivetrainMath.CalculateMotorCurveTorque(15f, 8500f, 30000f, 0.265f, 7f, 16.5f, 18f),
    "power-limited torque near cruise speed");
Expect(
    (30000f / (17.25f / 3.6f)) * 0.265f / 7f * 0.5f,
    DrivetrainMath.CalculateMotorCurveTorque(17.25f, 8500f, 30000f, 0.265f, 7f, 16.5f, 18f),
    "governed torque near top speed");
Expect(
    0f,
    DrivetrainMath.CalculateMotorCurveTorque(18f, 8500f, 30000f, 0.265f, 7f, 16.5f, 18f),
    "motor torque cutoff");
Expect(1f, DrivetrainMath.CalculateSpeedLimiter(16.5f, 16.5f, 18f), "full torque before governor");
Expect(0.5f, DrivetrainMath.CalculateSpeedLimiter(17.25f, 16.5f, 18f), "governor torque fade");
Expect(0f, DrivetrainMath.CalculateSpeedLimiter(18f, 16.5f, 18f), "governor cutoff");
Expect(60f, Constants.PalletStorageUiVerticalOffset, "pallet storage UI vertical offset");
Expect(1f, Constants.PalletTransitAccessOffset, "pallet employee access offset");
Expect(-0.85f, Constants.PalletTransitAccessHeight, "pallet employee access height");
Expect(0.25f, Constants.PalletTransitLinkHeight, "pallet transit link height");

if (Constants.PalletItemId != "pallet")
    throw new InvalidOperationException("The pallet item ID must be 'pallet'.");

Expect(20000f, Constants.VehiclePrice, "forklift dealership price");

if (Constants.DealershipChoiceLabel != "FORKLIFT")
    throw new InvalidOperationException("The dealership choice label must match the vehicle code.");

if (Constants.DealershipChoiceText != "The Forklift (<PRICE>)")
    throw new InvalidOperationException("The dealership choice must retain the native price placeholder.");

Console.WriteLine("PASS|S1Forklift.LiftMathVerifier|37 scenarios");
