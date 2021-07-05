using Godot;
using System;

public class Character : KinematicBody2D
{
	public enum MovementType
	{
		Idle,
		Walk,
		Run,
		Block
	}

	#region ExportVaribles

	[Export]
	public float Gravity = 500f;
	
	
	[Export(PropertyHint.Layers2dPhysics)]
	/**
	 * Character's collision layer will be set to this value once they are dead
	 */
	public uint DeathPhysicsLayer;

	[Export(PropertyHint.Layers2dPhysics)]
	/**
	 * Character's collision mask will be set to this value once they are dead
	 */
	public uint DeathPhysicsMask;

	[Export]
	public float MovementSpeed = 100f;

	[Export]
	public float JumpForce = -300f;

	[Export]
	public bool ControlledByPlayed = true;

	[Export]
	public float AttackResetTime = 0.5f;

	[Export]
	public int Health = 5;

	#endregion


	#region AttackVariables
	//attack system works in steps 1-punch,2-uppercut,3-kick, then it resets
	protected int currentAttackCount = 0;

	protected bool isAttacking = false;

	protected bool isBeingDamaged = false;
	#endregion

	protected Vector2 velocity = Vector2.Zero;

	public Vector2 CharacterVeloctiy => velocity;

	protected MovementType movementState = MovementType.Idle;

	public MovementType CurrentMovementState => movementState;

	#region Nodes

	protected AnimatedSprite animatedSprite;

	protected Timer attackResetTimer;

	private bool _firstAttack = true;

	protected Area2D attackArea;

	#endregion

	protected bool dead = false;

	public bool Dead => dead;


	public virtual bool CanUpdateAnimation()
	{
		return !isAttacking && !isBeingDamaged && !Dead;
	}

	public void BeDamaged(Node2D damager, int attackType, int damage = 1)
	{
		if (!Dead)
		{

			if(damager != null)
            {
				//we need to force chracter to turn towards the attacker
				//this way there is no need to have as many animations
                if ((Position.x - damager.Position.x) > 0)
                {
					//on the right
					SetCharacterMovementScale(new Vector2(-1, 1));
                }
                else
                {
					//on the left
					SetCharacterMovementScale(new Vector2(1, 1));
				}
            }
			Health -= damage;
			isBeingDamaged = true;
			animatedSprite?.Play(GetDamageAnimation(attackType));

			if (Health < 0)
			{
				Die();
			}
		}
	}

	protected string GetAttackAnimation()
	{
		switch(currentAttackCount)
		{
			case 1:
				return "Uppercut";
			case 2:
				return "Kick";
			default:
				return "Punch";
		}
	}

	protected static string GetDamageAnimation(int attackType)
	{
		switch (attackType)
		{
			case 0:
				return "Hit_Uppercut";
			case 2:
				return "Hit_Heavy";
			case 1:
				return "Hit_Middle";
			default:
				return "";
		}
	}

	protected void SetCharacterMovementScale(Vector2 scale)
	{
		animatedSprite.Scale = scale;
		attackArea.Scale = scale;
	}

	public virtual void Die()
	{
		if(!Dead)
		{
			dead = true;

			//disable collsion(We don't just set to 0 to avoid characters falling thru the ground)
			CollisionMask = DeathPhysicsMask;
			CollisionLayer = DeathPhysicsLayer;

			animatedSprite?.Play("Knockdown");
		}
	}

	public override void _Ready()
	{
		base._Ready();
		animatedSprite = GetNode<AnimatedSprite>("AnimatedSprite") ?? throw new NullReferenceException("Fatal Error! All characters need to have animated sprite");
		attackArea = GetNode<Area2D>("AttackArea") ?? throw new NullReferenceException("Fatal Error! All characters need to have attack area");
		if (attackResetTimer == null)
		{
			attackResetTimer = new Timer();
			attackResetTimer.WaitTime = AttackResetTime;
			attackResetTimer.Connect("timeout", this, "ResetAttack");
			AddChild(attackResetTimer);
		}
		GetNode<Camera2D>("Camera2D").Current = ControlledByPlayed;


	}

	protected void ResetAttack()
	{
		//GD.Print("ResetAttack");
		currentAttackCount = 0;
		attackResetTimer.Stop();
	}

	protected virtual void UpdateInput(float delta)
	{
		velocity.x = 0;
		if (CanUpdateAnimation())
		{
			if (Input.IsActionPressed("move_right"))
			{
				velocity.x += MovementSpeed;
			}

			if (Input.IsActionPressed("move_left"))
			{
				velocity.x -= MovementSpeed;
			}

			if (Input.IsActionJustPressed("jump") && IsOnFloor())
			{
				velocity.y += JumpForce;
			}
		}

		if (Input.IsActionJustPressed("attack") && !isAttacking)
		{
			
			animatedSprite.Play(GetAttackAnimation());
			
			isAttacking = true;
			_firstAttack = false;

			var enemies = attackArea.GetOverlappingBodies();
			foreach(Node2D enemy in enemies)
			{
				if(enemy.IsInGroup("Character"))
				{
					enemy.Call("BeDamaged",this,currentAttackCount, 1/*damage*/);
				}
			}
			currentAttackCount++;
			attackResetTimer.Paused = true;
		}
	}

	protected void UpdateMovementState()
	{
		if (Mathf.Abs(velocity.x) > 1)
		{
			movementState = MovementType.Walk;
		}
		else
		{
			movementState = MovementType.Idle;
		}
	}

	public override void _PhysicsProcess(float delta)
	{
		base._PhysicsProcess(delta);

		if (ControlledByPlayed)
		{
			UpdateInput(delta);
		}
		UpdateMovementState();

		velocity.y += Gravity * delta;

		velocity = MoveAndSlide(velocity, Vector2.Up);

		if (CanUpdateAnimation())
		{
			switch (CurrentMovementState)
			{
				case MovementType.Idle:
					if (animatedSprite.Animation != "Idle_Normal")
					{
						//animatedSprite.Scale = new Vector2(1, 1);
						animatedSprite.Play("Idle_Normal");
					}
					break;
				case MovementType.Walk:
					if (animatedSprite.Animation != "Walk_Battle")
					{
						animatedSprite.Play("Walk_Battle");
					}
					SetCharacterMovementScale(new Vector2(velocity.x / Mathf.Abs(velocity.x), 1));
					break;
				case MovementType.Run:
					if (animatedSprite.Animation != "Run_Normal")
					{
						animatedSprite.Animation = "Run_Normal";
					}
					SetCharacterMovementScale(new Vector2(velocity.x / Mathf.Abs(velocity.x), 1));
					break;
				default:
					break;
			}
		}
	}
	private void _on_AnimatedSprite_animation_finished()
	{
		isAttacking = false;
		isBeingDamaged = false;
		attackResetTimer.Paused = false;
		if (attackResetTimer.IsStopped() || _firstAttack)
		{
			attackResetTimer.Start();
		}
	}
}
