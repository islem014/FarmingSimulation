using UnityEngine;

namespace FarmingSimulation
{
    [RequireComponent(typeof(CharacterController), typeof(AnimalStats))]
    public class FarmAnimalBehaviour : MonoBehaviour
    {
        [SerializeField] protected AnimalEnclosure enclosure;
        [SerializeField] protected Animator animator;
        [SerializeField] protected FeedingTrough feedingTrough;
        [SerializeField] protected WaterTrough waterTrough;
        [SerializeField, Min(0.1f)] protected float moveSpeed = 0.7f;
        [SerializeField, Min(1f)] protected float turnSpeed = 120f;
        [SerializeField, Min(0.1f)] protected float idleDuration = 3f;
        [Header("Personality — gameplay tuning, not an animal science model")]
        [SerializeField] protected string personality = "Calm";
        [SerializeField, Min(0.9f)] protected float wanderDistance = 3f;
        [SerializeField, Min(1f)] protected float restTravelTimeout = 10f;
        [SerializeField, Range(1, 100)] protected float hungryThreshold = 65f;
        [SerializeField, Range(0, 99)] protected float satisfiedThreshold = 20f;
        [SerializeField, Min(0.1f)] protected float portionInterval = 2f;
        [SerializeField, Min(1f)] protected float hungerPerPortion = 20f;
        [SerializeField, Min(0.5f)] protected float foodRetryInterval = 3f;
        [Header("Hydration")]
        [SerializeField, Range(1, 100)] protected float thirstyThreshold = 65f;
        [SerializeField, Range(0, 99)] protected float hydratedThreshold = 20f;
        [SerializeField, Min(0.1f)] protected float drinkInterval = 2f;
        [SerializeField, Min(1)] protected float thirstPerDrink = 20f;
        [SerializeField] protected int randomSeed = 123;
        [Header("Species consumption and animation")]
        [SerializeField, Min(1)] protected int foodPortionsPerBite = 1;
        [SerializeField, Min(1)] protected int waterPortionsPerSip = 1;
        [SerializeField] protected bool useMovementAnimation;
        [SerializeField] protected bool playFeedingJump = true;
        protected CharacterController controller;
        protected AnimalStats stats;
        protected System.Random random;
        protected Vector3 destination;
        protected float stateTime, verticalSpeed;
        protected bool wantsFood, usingApproachPoint, mealAnimated;
        protected bool wantsWater;
        protected bool wantsRest;
        protected float nextFoodAttempt, nextWaterAttempt;
        protected string feedingStatus = "Refill the trough to feed this animal";
        public AnimalStats Stats => stats;
        public float HungryThreshold => hungryThreshold;
        public float ThirstyThreshold => thirstyThreshold;
        public string FeedingStatus => feedingStatus;
        public event System.Action FeedbackChanged;
        protected static readonly int Fed = Animator.StringToHash("Fed");
        protected static readonly int Moving = Animator.StringToHash("Moving");
        protected const float ArrivalDistance = 0.18f;

        protected void Awake()
        {
            ValidateSettings();
            controller = GetComponent<CharacterController>();
            stats = GetComponent<AnimalStats>();
            random = new System.Random(randomSeed);
            if (enclosure == null || animator == null)
            {
                Debug.LogError("FarmAnimalBehaviour needs its enclosure and Animator assigned.", this);
                enabled = false;
                return;
            }
            animator.applyRootMotion = false;
            SetState(AnimalBehaviour.Idle, idleDuration);
        }

        protected void OnEnable()
        {
            if (feedingTrough != null) feedingTrough.FoodChanged += OnFoodChanged;
            if (waterTrough != null) waterTrough.WaterChanged += OnFoodChanged;
        }
        protected void OnDisable()
        {
            if (feedingTrough != null) feedingTrough.FoodChanged -= OnFoodChanged;
            if (waterTrough != null) waterTrough.WaterChanged -= OnFoodChanged;
        }
        protected void OnFoodChanged() => FeedbackChanged?.Invoke();
        protected void OnValidate() => ValidateSettings();
        protected void ValidateSettings()
        {
            hungryThreshold = Mathf.Clamp(hungryThreshold, 1f, 100f);
            satisfiedThreshold = Mathf.Clamp(satisfiedThreshold, 0f, hungryThreshold - 1f);
            portionInterval = Mathf.Max(0.1f, portionInterval);
            hungerPerPortion = Mathf.Max(1f, hungerPerPortion);
            foodRetryInterval = Mathf.Max(0.5f, foodRetryInterval);
            thirstyThreshold = Mathf.Clamp(thirstyThreshold, 1f, 100f);
            hydratedThreshold = Mathf.Clamp(hydratedThreshold, 0f, thirstyThreshold - 1f);
            drinkInterval = Mathf.Max(0.1f, drinkInterval);
            thirstPerDrink = Mathf.Max(1f, thirstPerDrink);
            foodPortionsPerBite = Mathf.Max(1, foodPortionsPerBite);
            waterPortionsPerSip = Mathf.Max(1, waterPortionsPerSip);
            moveSpeed = Mathf.Max(.1f, moveSpeed);
            idleDuration = Mathf.Max(.1f, idleDuration);
            wanderDistance = Mathf.Max(.9f, wanderDistance);
            restTravelTimeout = Mathf.Max(1f, restTravelTimeout);
        }

        protected void SetState(AnimalBehaviour state, float seconds, string status = null)
        {
            stateTime = seconds;
            if (status != null) feedingStatus = status;
            stats.SetBehaviour(state);
            FeedbackChanged?.Invoke();
        }

        protected void Update()
        {
            stateTime -= Time.deltaTime;
            UpdatePriorities();

            Vector3 velocity = Vector3.zero;
            switch (stats.Behaviour)
            {
                case AnimalBehaviour.Hungry:
                    if (stateTime <= 0f) PlanFoodRoute();
                    break;
                case AnimalBehaviour.SeekingFood:
                    velocity = SeekFood();
                    break;
                case AnimalBehaviour.Eating:
                    EatPortion();
                    break;
                case AnimalBehaviour.Thirsty:
                    if (stateTime <= 0f && Time.time >= nextWaterAttempt && WaterAvailable) PlanWaterRoute();
                    break;
                case AnimalBehaviour.SeekingWater:
                    velocity = SeekWater();
                    break;
                case AnimalBehaviour.Drinking:
                    DrinkPortion();
                    break;
                case AnimalBehaviour.NeedsCare:
                    break; // Rest, but priorities still allow food/water and recovery at any health.
                case AnimalBehaviour.SeekingRest:
                    velocity = SeekRest();
                    break;
                case AnimalBehaviour.Resting:
                    break; // No planar movement; gravity still keeps the controller grounded.
                case AnimalBehaviour.Idle:
                    if (stateTime <= 0f) ChooseDestination();
                    break;
                case AnimalBehaviour.Wander:
                    velocity = MoveTowardDestination(false);
                    break;
            }

            // Preserve the original enclosure clamp, CharacterController and gravity.
            Vector3 next = enclosure.ClampPosition(transform.position + velocity * Time.deltaTime);
            Vector3 step = next - transform.position;
            step.y = 0f;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(verticalSpeed - 20f * Time.deltaTime, -30f);
            Vector3 beforeMove = transform.position;
            CollisionFlags contacts = controller.Move(step + Vector3.up * verticalSpeed * Time.deltaTime);
            Vector3 displacement = transform.position - beforeMove; displacement.y = 0f;
            bool resting = stats.Behaviour == AnimalBehaviour.Resting || stats.Behaviour == AnimalBehaviour.NeedsCare;
            stats.AdvanceEnergy(displacement.magnitude, resting && displacement.sqrMagnitude < .000001f ? Time.deltaTime : 0f);
            if (useMovementAnimation)
            {
                Vector3 moved = transform.position - beforeMove; moved.y = 0f;
                animator.SetBool(Moving, moved.sqrMagnitude > 0.000001f);
            }
            if ((contacts & CollisionFlags.Sides) != 0)
            {
                if (stats.Behaviour == AnimalBehaviour.Wander) SetState(AnimalBehaviour.Idle, idleDuration);
                else if (stats.Behaviour == AnimalBehaviour.SeekingFood) WaitForFood("Path blocked - waiting to retry");
                else if (stats.Behaviour == AnimalBehaviour.SeekingWater) WaitForWater("Water path blocked - waiting to retry");
                else if (stats.Behaviour == AnimalBehaviour.SeekingRest) BeginRest();
            }
        }

        protected bool FoodAvailable => feedingTrough != null && feedingTrough.HasPortions(foodPortionsPerBite)
            && feedingTrough.FeedingPoint != null && IsOwnResource(feedingTrough);
        protected bool WaterAvailable => waterTrough != null && waterTrough.HasPortions(waterPortionsPerSip)
            && waterTrough.DrinkingPoint != null && IsOwnResource(waterTrough);

        protected bool IsOwnResource(Component resource)
        {
            var owner = resource.GetComponentInParent<AnimalEnclosure>();
            // Unparented editor/test resources still require in-bounds use points and close reach.
            return owner == null || owner == enclosure;
        }

        protected void UpdatePriorities()
        {
            // Latches provide hysteresis and preserve unfinished meals/drinks after a trough empties.
            if (stats.Hunger >= hungryThreshold) wantsFood = true;
            else if (stats.Hunger <= satisfiedThreshold) wantsFood = false;
            if (stats.Thirst >= thirstyThreshold) wantsWater = true;
            else if (stats.Thirst <= hydratedThreshold) wantsWater = false;
            if (stats.Tired) wantsRest = true;
            else if (stats.Recovered) wantsRest = false;
            bool waterReady = wantsWater && WaterAvailable && Time.time >= nextWaterAttempt;
            bool foodReady = wantsFood && FoodAvailable && Time.time >= nextFoodAttempt;
            bool foodActive = stats.Behaviour == AnimalBehaviour.SeekingFood || stats.Behaviour == AnimalBehaviour.Eating;
            bool waterActive = stats.Behaviour == AnimalBehaviour.SeekingWater || stats.Behaviour == AnimalBehaviour.Drinking;

            // Finish the current activity; only severe thirst interrupts a food activity.
            if (waterActive) return;
            if (foodActive)
            {
                if (stats.SeverelyThirsty && waterReady) TryStartWater(true);
                return;
            }
            bool restActive = stats.Behaviour == AnimalBehaviour.Resting || stats.Behaviour == AnimalBehaviour.SeekingRest;
            // Severe reachable needs interrupt rest. Failed routes back off rather than cancelling
            // recovery every frame; an empty trough never grants food or hydration.
            if (stats.SeverelyThirsty && waterReady && TryStartWater(true)) return;
            if (stats.SeverelyHungry && foodReady)
            {
                PlanFoodRoute();
                if (stats.Behaviour == AnimalBehaviour.SeekingFood) return;
                if (restActive) BeginRest();
            }
            if (restActive && stats.NeedsTreatment)
            {
                SetState(AnimalBehaviour.NeedsCare, foodRetryInterval, "Resting - treatment needed");
                return;
            }
            if (wantsRest && !stats.NeedsTreatment)
            {
                if (!restActive) PlanRest();
                return;
            }
            if (restActive)
                SetState(AnimalBehaviour.Idle, idleDuration, "Recovered - checking other needs");
            // Try water first when both are waiting. A blocked resource has a timed backoff,
            // allowing the other need to be addressed rather than retrying every frame.
            if (waterReady) { PlanWaterRoute(); return; }
            if (foodReady) { PlanFoodRoute(); return; }

            bool waiting = stats.Behaviour == AnimalBehaviour.Hungry || stats.Behaviour == AnimalBehaviour.Thirsty
                || stats.Behaviour == AnimalBehaviour.NeedsCare;
            if (stats.NeedsTreatment)
            {
                if (stats.Behaviour != AnimalBehaviour.NeedsCare)
                    SetState(AnimalBehaviour.NeedsCare, foodRetryInterval, "Resting - treatment needed");
            }
            else if (wantsWater)
            {
                if (!waiting || stateTime <= 0f)
                    SetState(AnimalBehaviour.Thirsty, foodRetryInterval,
                        WaterAvailable ? "Water route blocked - waiting" : "Thirsty - refill water trough");
            }
            else if (wantsFood)
            {
                if (!waiting || stateTime <= 0f)
                    SetState(AnimalBehaviour.Hungry, foodRetryInterval,
                        FoodAvailable ? "Food route blocked - waiting" : "Hungry - refill food trough");
            }
            else if (waiting) SetState(AnimalBehaviour.Idle, idleDuration, "Needs stable - ready to wander");
        }

        protected void WaitForFood(string message)
        {
            nextFoodAttempt = Time.time + foodRetryInterval;
            // Stay Hungry between timed retries; never bounce into Eating at an empty trough.
            SetState(AnimalBehaviour.Hungry, foodRetryInterval, message);
        }

        protected void WaitForWater(string message)
        {
            nextWaterAttempt = Time.time + foodRetryInterval;
            SetState(AnimalBehaviour.Thirsty, foodRetryInterval, message);
        }

        protected void PlanWaterRoute() => TryStartWater(false);

        protected bool RejectWaterRoute(string message, bool preserveFood)
        {
            nextWaterAttempt = Time.time + foodRetryInterval;
            if (!preserveFood) WaitForWater(message);
            return false;
        }

        protected bool TryStartWater(bool preserveFood)
        {
            if (!WaterAvailable) return RejectWaterRoute("Thirsty - refill water trough", preserveFood);
            Vector3 goal = GroundPoint(waterTrough.DrinkingPoint);
            if (!InsideBounds(goal)) return RejectWaterRoute("Drinking point outside enclosure", preserveFood);
            Vector3 nextDestination = goal;
            bool needsApproach = false;
            if (!PathIsClear(goal))
            {
                var approach = waterTrough.ApproachPoint;
                if (approach == null) return RejectWaterRoute("Water path blocked - waiting to retry", preserveFood);
                Vector3 via = GroundPoint(approach);
                if (!InsideBounds(via) || !PathIsClear(via) || !PathIsClearFrom(via, goal))
                    return RejectWaterRoute("Water path blocked - waiting to retry", preserveFood);
                nextDestination = via;
                needsApproach = true;
            }
            // Do not replace an ongoing food destination until a water route succeeds.
            destination = nextDestination;
            usingApproachPoint = needsApproach;
            SetState(AnimalBehaviour.SeekingWater, 30f, "Moving to the water trough");
            return true;
        }

        protected Vector3 SeekWater()
        {
            if (!WaterAvailable) { WaitForWater("Thirsty - refill water trough"); return Vector3.zero; }
            if (PlanarDistance(destination) <= ArrivalDistance)
            {
                if (usingApproachPoint)
                {
                    usingApproachPoint = false;
                    destination = GroundPoint(waterTrough.DrinkingPoint);
                    stateTime = 30f;
                }
                else SetState(AnimalBehaviour.Drinking, drinkInterval, "Drinking from the water trough");
                return Vector3.zero;
            }
            return MoveTowardDestination(true);
        }

        protected void DrinkPortion()
        {
            if (stats.Thirst <= hydratedThreshold) { FinishDrinking(); return; }
            if (!WaterAvailable) { WaitForWater("Thirsty - refill water trough"); return; }
            Vector3 point = GroundPoint(waterTrough.DrinkingPoint);
            if (PlanarDistance(point) > ArrivalDistance + 0.1f || !PathIsClear(point))
            { WaitForWater("Drinking spot blocked - waiting to retry"); return; }
            Vector3 facing = waterTrough.transform.position - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f) RotateToward(facing);
            if (stateTime > 0f) return;
            stateTime = drinkInterval;
            if (!waterTrough.TryConsumePortions(waterPortionsPerSip)) { WaitForWater("Thirsty - refill water trough"); return; }
            stats.ReduceThirst(thirstPerDrink); // Only after paying one complete water portion.
            if (stats.Thirst <= hydratedThreshold) FinishDrinking();
            else if (!WaterAvailable) WaitForWater("Thirsty - refill water trough");
        }

        protected void FinishDrinking()
        {
            wantsWater = false;
            SetState(AnimalBehaviour.Idle, idleDuration, "Hydrated - checking other needs");
        }

        protected Vector3 GroundPoint(Transform point)
        {
            Vector3 result = point.position;
            result.y = transform.position.y;
            return result;
        }

        protected bool InsideBounds(Vector3 point) =>
            (enclosure.ClampPosition(point) - point).sqrMagnitude < 0.001f;

        protected void PlanFoodRoute()
        {
            if (!FoodAvailable)
            {
                WaitForFood(feedingTrough == null ? "No trough assigned" : "Hungry - refill the trough");
                return;
            }
            Vector3 goal = GroundPoint(feedingTrough.FeedingPoint);
            if (!InsideBounds(goal)) { WaitForFood("Feeding point is outside the enclosure"); return; }
            usingApproachPoint = false;
            destination = goal;
            if (!PathIsClear(goal))
            {
                // One optional waypoint gives this small pen a simple route around the trough.
                var approach = feedingTrough.ApproachPoint;
                if (approach == null) { WaitForFood("Path blocked - waiting to retry"); return; }
                Vector3 via = GroundPoint(approach);
                if (!InsideBounds(via) || !PathIsClear(via) || !PathIsClearFrom(via, goal))
                { WaitForFood("Path blocked - waiting to retry"); return; }
                destination = via;
                usingApproachPoint = true;
            }
            SetState(AnimalBehaviour.SeekingFood, 30f, "Moving to the feeding trough");
        }

        protected Vector3 SeekFood()
        {
            if (!FoodAvailable) { WaitForFood("Hungry - refill the trough"); return Vector3.zero; }
            if (PlanarDistance(destination) <= ArrivalDistance)
            {
                if (usingApproachPoint)
                {
                    usingApproachPoint = false;
                    destination = GroundPoint(feedingTrough.FeedingPoint);
                    stateTime = 30f;
                }
                else
                {
                    mealAnimated = false;
                    SetState(AnimalBehaviour.Eating, portionInterval, "Eating from the trough");
                }
                return Vector3.zero;
            }
            return MoveTowardDestination(true);
        }

        protected void EatPortion()
        {
            if (stats.Hunger <= satisfiedThreshold) { FinishMeal(); return; }
            if (!FoodAvailable) { WaitForFood("Hungry - refill the trough"); return; }
            Vector3 point = GroundPoint(feedingTrough.FeedingPoint);
            // No remote meals: moving the animal/trough or blocking its feeding spot interrupts it.
            if (PlanarDistance(point) > ArrivalDistance + 0.1f || !PathIsClear(point))
            { WaitForFood("Feeding spot blocked - waiting to retry"); return; }
            Vector3 facing = feedingTrough.transform.position - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f) RotateToward(facing);
            if (stateTime > 0f) return;
            stateTime = portionInterval;
            if (!feedingTrough.TryConsumePortions(foodPortionsPerBite)) { WaitForFood("Hungry - refill the trough"); return; }
            // Only this path reduces hunger: successful food consumption always comes first.
            stats.ReduceHunger(hungerPerPortion);
            if (!mealAnimated && playFeedingJump) { animator.SetTrigger(Fed); mealAnimated = true; }
            if (stats.Hunger <= satisfiedThreshold) FinishMeal();
            else if (!FoodAvailable) WaitForFood("Hungry - refill the trough");
        }

        protected void FinishMeal()
        {
            wantsFood = false;
            SetState(AnimalBehaviour.Idle, idleDuration, "Satisfied - returning to wandering");
        }

        protected float PlanarDistance(Vector3 point)
        {
            Vector3 offset = point - transform.position; offset.y = 0f;
            return offset.magnitude;
        }

        protected Vector3 MoveTowardDestination(bool seeking)
        {
            Vector3 offset = destination - transform.position; offset.y = 0f;
            if (offset.magnitude <= ArrivalDistance || stateTime <= 0f
                || !PathIsClear(transform.position + offset.normalized * Mathf.Min(0.8f, offset.magnitude)))
            {
                if (seeking && stats.Behaviour == AnimalBehaviour.SeekingWater) WaitForWater("Water path blocked - waiting to retry");
                else if (seeking) WaitForFood("Path blocked - waiting to retry");
                else SetState(AnimalBehaviour.Idle, idleDuration);
                return Vector3.zero;
            }
            RotateToward(offset);
            return offset.normalized * Mathf.Min(moveSpeed, offset.magnitude / Mathf.Max(Time.deltaTime, 0.001f));
        }

        protected void RotateToward(Vector3 offset) => transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(offset), turnSpeed * Time.deltaTime);

        protected void ChooseDestination()
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 candidate = enclosure.SampleDestination(random, transform.position.y);
                candidate = enclosure.ClampPosition(transform.position + Vector3.ClampMagnitude(candidate-transform.position, wanderDistance));
                if (Vector3.Distance(candidate, transform.position) > 0.8f && PathIsClear(candidate))
                {
                    destination = candidate;
                    SetState(AnimalBehaviour.Wander, 15f);
                    return;
                }
            }
            SetState(AnimalBehaviour.Idle, idleDuration);
        }

        protected void PlanRest()
        {
            // Prefer resting in the already occupied, collision-free location, away from supplies.
            if (SafeRestPoint(transform.position, true)) { BeginRest(); return; }
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var p = enclosure.SampleDestination(random, transform.position.y);
                if (PlanarDistance(p) <= 3f && SafeRestPoint(p, true) && PathIsClear(p))
                {
                    destination = p;
                    SetState(AnimalBehaviour.SeekingRest, restTravelTimeout, "Moving to a quiet resting spot");
                    return;
                }
            }
            // Bounded search: rest where the controller already stands rather than loop forever.
            BeginRest();
        }

        protected bool SafeRestPoint(Vector3 p, bool avoidSupplies)
        {
            if (!InsideBounds(p)) return false;
            if (avoidSupplies && ((feedingTrough != null && feedingTrough.FeedingPoint != null
                && Vector3.Distance(p, GroundPoint(feedingTrough.FeedingPoint)) < 1f)
                || (waterTrough != null && waterTrough.DrinkingPoint != null
                && Vector3.Distance(p, GroundPoint(waterTrough.DrinkingPoint)) < 1f))) return false;
            foreach (var hit in Physics.OverlapCapsule(p + Vector3.up*(controller.radius+.06f),
                p + Vector3.up*(controller.height-controller.radius+.06f), controller.radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) return false;
            return true;
        }

        protected void BeginRest() => SetState(AnimalBehaviour.Resting, 0f, "Resting - recovering energy");

        protected Vector3 SeekRest()
        {
            if (PlanarDistance(destination) <= ArrivalDistance || stateTime <= 0f || !PathIsClear(destination))
            { BeginRest(); return Vector3.zero; }
            Vector3 direction = destination-transform.position; direction.y = 0f;
            RotateToward(direction);
            return direction.normalized * Mathf.Min(moveSpeed, direction.magnitude/Mathf.Max(Time.deltaTime,.001f));
        }

        protected bool PathIsClear(Vector3 point) => PathIsClearFrom(transform.position, point);
        protected bool PathIsClearFrom(Vector3 origin, Vector3 point)
        {
            Vector3 offset = point - origin; offset.y = 0f;
            float clearance = controller.radius + 0.05f;
            if (offset.sqrMagnitude < 0.0001f) return true;
            return !PlayerPhysicsQueries.SphereCast(origin + Vector3.up * (clearance + 0.1f),
                clearance, offset.normalized, offset.magnitude, transform, out _);
        }
    }
}

