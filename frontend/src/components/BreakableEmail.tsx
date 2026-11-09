/** An email address that wraps before the "@" when it must, rather than mid-word ("…exampl / e"). */
export function BreakableEmail({ email }: { email: string }) {
  const at = email.indexOf('@');
  if (at <= 0) return <>{email}</>;
  return (
    <>
      {email.slice(0, at)}
      <wbr />
      {email.slice(at)}
    </>
  );
}
