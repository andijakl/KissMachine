using System;

namespace KissMachineKinect.Models
{
    public class Vec3
    {
        public float X;
        public float Y;
        public float Z;

        public static Vec3 Zero => new Vec3(0, 0, 0);

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public static float Dot(Vec3 left, Vec3 right)
        {
            return (left.X * right.X + left.Y * right.Y + left.Z * right.Z);
        }

        public static Vec3 operator -(Vec3 left, Vec3 right)
        {
            return new Vec3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static Vec3 operator +(Vec3 left, Vec3 right)
        {
            return new Vec3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static Vec3 operator *(Vec3 left, float value)
        {
            return new Vec3(left.X * value, left.Y * value, left.Z * value);
        }

        public static Vec3 operator *(float value, Vec3 left)
        {
            return left * value;
        }

        public static Vec3 operator /(Vec3 left, float value)
        {
            return new Vec3(left.X / value, left.Y / value, left.Z / value);
        }
    }
}
