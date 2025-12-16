using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ubco.ovilab.uxf.extensions
{
    /// <summary>
    /// Object to parse json data from experiment server. An extension
    /// of this class can be used as the generic type in <see
    /// cref="ExperimentManager"/>. Data from experiment server would
    /// automatically add the name and participant_index. The
    /// calibrationName would need to be set in each block in the
    /// experiment server config file.
    /// </summary>
    public class BlockData
    {
        private bool uniqueSeedComputed = false;
        private int uniqueSeed;

        /// <summary>
        /// The name of the block.
        /// </summary>
        public string name;
        /// <summary>
        /// The participant index.
        /// </summary>
        public int participant_index;
        /// <summary>
        /// The calibration name.
        /// </summary>
        public string calibrationName;
        /// <summary>
        /// The block index in the list of blocks configured in
        /// experiment-server.
        /// </summary>
        public int block_id;

        public override string ToString()
        {
            return $"Name: {name}\n" +
                $"Participant Index: {participant_index}\n" +
                $"Calibration name: {calibrationName}\n" +
                $"Block ID:  {block_id}\n";
        }

        /// <summary>
        /// Computes a deterministic 32-bit seed that uniquely
        /// identifies the condition represented by the public
        /// instance fields of this BlockData instance.  The method
        /// builds a stable, canonical byte representation of each
        /// public instance field (fields are taken from GetType()
        /// with BindingFlags.Instance | BindingFlags.Public |
        /// BindingFlags.FlattenHierarchy and sorted by field name),
        /// hashes that representation with SHA-256, and folds part of
        /// the hash into a 32-bit signed integer.
        /// </summary>
        /// <returns>
        /// A deterministic int derived from the SHA-256 hash of the
        /// canonical field representation. The value may be
        /// negative. The same set of field names and values (with
        /// identical string/ToString() outputs and numeric encodings)
        /// will always produce the same seed.
        /// </returns>
        /// <remarks>
        /// This is a GPT generated function.
        /// The computed seed is cached after the first call and
        /// returned on subsequent calls. The initial computation is
        /// thread-safe (uses double-checked locking).  The canonical
        /// representation encodes field names and values using UTF-8
        /// for strings, fixed-size little-endian byte forms for
        /// numeric types, a single marker for nulls, and a
        /// type-qualified ToString() fallback for complex types.  If
        /// you override this method, preserve determinism if the
        /// result is intended to uniquely identify experimental
        /// conditions. This method is not intended for cryptographic
        /// use; it is intended only to generate a stable condition
        /// identifier from the current public instance fields.
        /// </remarks>
        public virtual int UniqueConditionSeed()
        {
            if (uniqueSeedComputed)
            {
                return uniqueSeed;
            }

            lock (this)
            {
                if (uniqueSeedComputed)
                {
                    return uniqueSeed;
                }

                FieldInfo[] fields = this.GetType().GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

                Array.Sort(fields, (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

                using (SHA256 sha = SHA256.Create())
                using (MemoryStream ms = new MemoryStream())
                {
                    Encoding enc = Encoding.UTF8;

                    foreach (FieldInfo field in fields)
                    {
                        object value;
                        try
                        {
                            value = field.GetValue(this);
                        }
                        catch
                        {
                            value = null;
                        }

                        // Field name to make representation stable
                        byte[] nameBytes = enc.GetBytes(field.Name);
                        ms.Write(nameBytes, 0, nameBytes.Length);
                        ms.WriteByte((byte)':');

                        if (value == null)
                        {
                            ms.WriteByte((byte)'N');
                            ms.WriteByte((byte)'\n');
                            continue;
                        }

                        Type t = value.GetType();

                        if (t == typeof(string))
                        {
                            String s = (string)value;
                            byte[] sb = enc.GetBytes(s);
                            ms.Write(sb, 0, sb.Length);
                        }
                        else if (t == typeof(float))
                        {
                            ms.Write(BitConverter.GetBytes((float)value), 0, 4);
                        }
                        else if (t == typeof(double))
                        {
                            ms.Write(BitConverter.GetBytes((double)value), 0, 8);
                        }
                        else if (t == typeof(decimal))
                        {
                            foreach (int part in decimal.GetBits((decimal)value))
                            {
                                ms.Write(BitConverter.GetBytes(part), 0, 4);
                            }
                        }
                        else if (t == typeof(bool))
                        {
                            ms.WriteByte((byte)(((bool)value) ? 1 : 0));
                        }
                        else if (t.IsPrimitive)
                        {
                            // canonicalize primitive numeric types by their 64-bit representation
                            long numeric = Convert.ToInt64(value);
                            ms.Write(BitConverter.GetBytes(numeric), 0, 8);
                        }
                        else
                        {
                            // fallback: include type and ToString to keep determinism
                            String s2 = value.GetType().FullName + ":" + value.ToString();
                            byte[] bs = enc.GetBytes(s2);
                            ms.Write(bs, 0, bs.Length);
                        }

                        ms.WriteByte((byte)'\n');
                    }

                    byte[] hash = sha.ComputeHash(ms.ToArray());

                    // Combine hash bytes into a 32-bit value. This is explicit and endianness-stable:
                    uint part0 = (uint)hash[0] | (uint)hash[1] << 8 | (uint)hash[2] << 16 | (uint)hash[3] << 24;
                    uint part1 = (uint)hash[4] | (uint)hash[5] << 8 | (uint)hash[6] << 16 | (uint)hash[7] << 24;
                    uint combined = part0 ^ part1; // fold entropy from two words

                    uniqueSeed = unchecked((int)combined); // allow negative seeds too
                    uniqueSeedComputed = true;
                    return uniqueSeed;
                }
            }
        }
    }
}
