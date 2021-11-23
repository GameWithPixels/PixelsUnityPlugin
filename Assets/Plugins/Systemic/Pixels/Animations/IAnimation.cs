
namespace Animations
{
	/// <summary>
	/// Defines the types of Animation Presets we have/support
	/// </summary>
	public enum AnimationType : byte
	{
		//![SkipEnumValue]
		Unknown = 0,
		//![Name("Simple Flashes")][DisplayOrder(0)]
		Simple,
		//![Name("Colorful Rainbow")][DisplayOrder(1)]
		Rainbow,
		//![Name("Color LED Pattern")][DisplayOrder(3)]
		Keyframed,
		//![Name("Gradient LED Pattern")][DisplayOrder(4)]
		GradientPattern,
		//![Name("Simple Gradient")][DisplayOrder(2)]
		Gradient,
	};

	/// <summary>
	/// Base class for animation presets. All presets have a few properties in common.
	/// Presets are stored in flash, so do not have methods or vtables or anything like that.
	/// </summary>
	public interface IAnimation
	{
		AnimationType type { get; set; }
		byte padding_type { get; set; } // to keep duration 16-bit aligned
		ushort duration { get; set; } // in ms
        AnimationInstance CreateInstance(DataSet.AnimationBits bits);
	};
}
