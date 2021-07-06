using Godot;
using System;

/**
 * Character not controlled by player
 */
public class AICharacter : Character
{
	[Export(PropertyHint.None, "How long will ai wait before turning around")]
	/**
	 * How long will ai wait before turning around
	 * */
	public float TurnAroundWaitTime = 1f;

	[Export]
	public bool WalkRight = true;

	[Export]
	public float AttackRepeatTime = 0.5f;

	[Export(PropertyHint.Layers2dPhysics)]
	public uint SightCastCollisionLayer = 0;

	#region Fight

	[Export]
	public bool CanBlock = false;

	[Export]
	/**
	 * To prevent ai being too hard to fight ai will have an "opening" after each attack
	 * Ai will only get back into block mode after this much time passes
	 */
	public float TimeBeforeBlock = 0.5f;

	#endregion

	/*
	 * Current target 
	 */
	protected PlayerCharacter target;

	/*
	 * Current target 
	 */
	public PlayerCharacter Target => target;

	protected bool canWalk = true;

	#region Areas

	protected Area2D groundDetection;

	protected Area2D attackDetectionArea;

	protected Area2D sightArea;

	#endregion


	protected Timer turnAroundWaitTimer;

	protected Timer attackRepeatTimer;

	protected Timer blockTimer;

	protected RandomNumberGenerator random = new RandomNumberGenerator();

	#region Debug

	[Export]
	public bool displayDebugInfo = false;

	protected RichTextLabel debug_AttackIndicationLabel;

	#endregion

	protected override bool CanMove => base.CanMove && canWalk;

	public override void _Ready()
	{
		base._Ready();
		groundDetection = GetNodeOrNull<Area2D>("FloorDetection") ?? throw new NullReferenceException("AI characters need to have ground detection Area2D to be able to walk");

		attackDetectionArea = GetNodeOrNull<Area2D>("AttackDetectionArea");
		if (attackDetectionArea == null)
		{
			GD.PrintErr("AI will not be able to fight because there is no attack detection area found");
		}

		turnAroundWaitTimer = new Timer();
		turnAroundWaitTimer.Connect("timeout", this, nameof(TurnAround));
		AddChild(turnAroundWaitTimer);

		attackRepeatTimer = new Timer();
		attackRepeatTimer.Connect("timeout", this, nameof(Attack));
		AddChild(attackRepeatTimer);

		if (displayDebugInfo)
		{
			debug_AttackIndicationLabel = GetNodeOrNull<RichTextLabel>("DebugNodes/AttackIndication");
		}

		if(CanBlock)
		{
			blockTimer = new Timer();
			blockTimer.Connect("timeout", this, nameof(StartBlocking));
			AddChild(blockTimer);
		}

		//there is no point in doing any checks because if there is no area it will never be called 
		sightArea = GetNodeOrNull<Area2D>("Sight");
	}

	public override void BeDamaged(Node2D damager, int attackType, int damage = 1)
	{
		base.BeDamaged(damager, attackType, damage);
		if (!attackRepeatTimer.IsStopped())
		{
			//if ai is allowed to block and is currently blocking -> we reset timer on hit
			//this way spamming attack will not work against blocking characters
			if (CanBlock && Blocking)
			{
				attackRepeatTimer.Start(AttackRepeatTime);
			}
			else
			{
				attackRepeatTimer.Paused = true;
			}
		}

	}
	protected override void SetCharacterMovementScale(Vector2 scale)
	{
		base.SetCharacterMovementScale(scale);
		if (Mathf.Abs(scale.x) >= 1 && Mathf.Abs(scale.y) >= 1)
		{
			groundDetection.Scale = scale;
			attackDetectionArea.Scale = scale;
			if(sightArea != null)
			{
				sightArea.Scale = scale;
			}
		}
	}

	public void TurnAround()
	{
		WalkRight = !WalkRight;
		canWalk = true;
		turnAroundWaitTimer.Stop();
	}

	public void UpdateAI()
	{
		if (target != null)
		{
			Physics2DDirectSpaceState spaceState = GetWorld2d().DirectSpaceState;
			if (spaceState != null)
			{
				var result = spaceState.IntersectRay(GlobalPosition, target.GlobalPosition, new Godot.Collections.Array { this }, SightCastCollisionLayer);
				if (result.Count > 0)
				{
					if(result["collider"] == target)
					{
						//we see the target				
					}
					else
					{
						//we don't see the target
					}
				}
			}
		}
	}

	public override void _PhysicsProcess(float delta)
	{
		//we only need to set the speed with which the charcater is walking
		velocity.x = CanMove ? MovementSpeed * (WalkRight ? 1 : -1) : 0;
		base._PhysicsProcess(delta);

		UpdateAI();
	}

	protected override void Attack()
	{
		if (!isBeingDamaged && !Dead)
		{
			if (blocking)
			{
				blocking = false;
			}
			base.Attack();
			isAttacking = true;
			//attack the player
			random.Randomize();

			currentAttackCount = random.RandiRange(0, 2);
			PlayAnimation(GetAttackAnimation());

			attackRepeatTimer?.Stop();

			foreach (PhysicsBody2D body in attackDetectionArea.GetOverlappingBodies())
			{
				if (body.IsInGroup("Player"))
				{
					body.Call("BeDamaged", this, currentAttackCount, 1);
				}
			}

			if (displayDebugInfo)
			{
				debug_AttackIndicationLabel.Visible = false;
				debug_AttackIndicationLabel.Text = "!";
			}
		}
	}

	private void _on_FloorDetection_body_entered(Node2D body)
	{
		//ground is the only ground considered good for walking
		if (!body.IsInGroup("Ground"))
		{
			canWalk = false;
			turnAroundWaitTimer.Start(TurnAroundWaitTime);
		}
		GD.Print(body.Name);
	}

	private void _on_FloorDetection_body_exited(Node2D body)
	{
		//if detection doesn't detect ground any more -> stop, don't touch it, call an adult
		if (body.IsInGroup("Ground") && groundDetection.GetOverlappingBodies().Count == 0)
		{
			canWalk = false;
			turnAroundWaitTimer?.Start(TurnAroundWaitTime);
			GD.Print("stop, don't touch it, call an adult");
		}
	}

	private void _on_AttackDetectionArea_body_entered(Node2D body)
	{
		//the ai is only an enemy of player
		if (body.IsInGroup("Character") && body.IsInGroup("Player") && !(body as Character).Dead/*ignore player's dead body*/)
		{
			Attack();
			canWalk = false;
		}
	}

	private void _on_AttackDetectionArea_body_exited(Node2D body)
	{
		//the ai is only an enemy of player
		if (body.IsInGroup("Character") && body.IsInGroup("Player"))
		{
			canWalk = true;
			attackRepeatTimer.Stop();
		}
	}

	protected void StartBlocking()
	{
		if (displayDebugInfo)
		{
			debug_AttackIndicationLabel.Visible = true;
			debug_AttackIndicationLabel.Text = "x";
		}
		blocking = true;
		blockTimer.Stop();
	}

	protected override void onAnimatedSpriteAnimationFinished(string animName)
	{
		if (isAttacking)
		{
			isAttacking = false;
			attackRepeatTimer.Start(AttackRepeatTime);
			if (displayDebugInfo)
			{
				if (!CanBlock)
				{
					debug_AttackIndicationLabel.Visible = false;
					debug_AttackIndicationLabel.Text = "!";
				}
				else
				{
					blockTimer.Start(TimeBeforeBlock);
				}
			}
		}
		if (isBeingDamaged && attackRepeatTimer.Paused)
		{
			attackRepeatTimer.Paused = false;
		}
		if (Dead)
		{
			QueueFree();
		}
		base.onAnimatedSpriteAnimationFinished(animName);
	}

	private void _on_Sight_body_entered(PhysicsBody2D body)
	{
		if(target == null)
		{
			if(body.IsInGroup("Character") && body.IsInGroup("Player") && !(body as Character).Dead/*ignore player's dead body*/)
			{
				target = body as PlayerCharacter;
			}
		}
	}

	protected virtual void OnTargetLost()
	{
		target = null;
		attackRepeatTimer.Stop();
		blocking = false;
	}

	private void _on_Sight_body_exited(PhysicsBody2D body)
	{
		if (target != null && body == target)
		{
			OnTargetLost();
		}
	}
}
