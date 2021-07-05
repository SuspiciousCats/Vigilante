using Godot;
using System;

/**
 * This class is base for every character in the game.
 * This class implements basic features like walking, attaking,being damaged etc.
 * */
public class Character : KinematicBody2D
{
	public enum MovementType
	{
		//Character is just standing and doing nothing
		Idle,
		//Character is walking with default speed
		Walk,
		//Character is walking fast
		Run,
		//Character is standing in block(guard) pose
		Block,
		//character is falling(but not dead or knocked down)
		Air
	}

	#region ExportVaribles

	[Export]
	/**
	 * How fast will character fall
	 */
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

	

	protected Area2D attackArea;

	#endregion

	protected bool dead = false;

	public bool Dead => dead;

	protected bool isPlayingAnim = false;

	//value of IsOnTheGround() on the last update
	protected bool lastIsOnTheGround = true;

	public virtual bool CanUpdateAnimation()
	{
		return !isAttacking && !isBeingDamaged && !Dead && !isPlayingAnim;
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
			PlayAnimation(GetDamageAnimation(attackType), true);

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

	protected virtual void SetCharacterMovementScale(Vector2 scale)
	{
		if (Mathf.Abs(scale.x) >= 1 && Mathf.Abs(scale.y) >= 1)
		{
			animatedSprite.Scale = scale;
			attackArea.Scale = scale;
		}
	}

	/**
	 * Plays animation while blocking default animation update
	 * Useful for playing one time animations (like getting hit or jumping)
	 * @animationName - Animation to plays
	 * @allowOverride - Allow playing new animation over already playing one
	 */
	protected void PlayAnimation(string animationName,bool allowOverride = false)
	{
		if(allowOverride || !isPlayingAnim)
		{
			isPlayingAnim = true;
			animatedSprite.Play(animationName);
		}
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

	protected virtual bool isRunning()
	{
		return false;
	}

	public override void _Ready()
	{
		base._Ready();
		animatedSprite = GetNode<AnimatedSprite>("AnimatedSprite") ?? throw new NullReferenceException("Fatal Error! All characters need to have animated sprite");
		attackArea = GetNode<Area2D>("AttackArea") ?? throw new NullReferenceException("Fatal Error! All characters need to have attack area");
	}

	protected void UpdateMovementState()
	{
		if (IsOnFloor())
		{
			if (Mathf.Abs(velocity.x) > 1)
			{
				movementState = isRunning() ? MovementType.Run : MovementType.Walk;
			}
			else
			{
				movementState = MovementType.Idle;
			}
		}
		else
		{
			movementState = MovementType.Air;
		}
	}

	protected virtual void Attack() { }

	public override void _PhysicsProcess(float delta)
	{
		base._PhysicsProcess(delta);

		

		if(lastIsOnTheGround != IsOnFloor() && IsOnFloor())//not using collision detection because this is simplier
		{
			//we just landed
			PlayAnimation("Jump_End");
		}

		UpdateMovementState();

		lastIsOnTheGround = IsOnFloor();

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

				case MovementType.Air:
					animatedSprite.Play("Air");
					break;

				default:
					break;
			}
		}
	}

	protected virtual void onAnimatedSpriteAnimationFinished(string animName) { }
	private void _on_AnimatedSprite_animation_finished()
	{
		onAnimatedSpriteAnimationFinished(animatedSprite.Animation);
		isAttacking = false;
		isBeingDamaged = false;

		isPlayingAnim = false;
	}
}
