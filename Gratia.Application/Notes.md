## Input DTOs

### `RegisterUserDto`
- **Use case**: Register new users
- **Fields**: `Username`, `Email`, `Password`
- **Layer**: API → Application

### `LoginDto`
- **Use case**: Authenticate user credentials
- **Fields**: `Email`, `Password`

### `UpdateUserDto`
- **Use case**: Update user settings/profile
- **Fields**: Could include `Username`, `Bio`, `Image`, `Email`, `Password`

### `ResetPasswordRequestDto`
- **Use case**: Trigger password reset email flow
- **Fields**: `Email`

### `SetNewPasswordDto`
- **Use case**: Accept new password after token verification
- **Fields**: `Token`, `NewPassword`

---

## Output DTOs

### `AuthUserDto`
- **Use case**: Return user info after successful login/register
- **Fields**: `Username`, `Email`, `Token`, optionally `Bio`, `Image`

### `UserProfileDto`
- **Use case**: Public or private user profile display
- **Fields**: Typically excludes sensitive info like tokens or emails (if public)

### `UserListDto`
- **Use case**: Admin view or user list endpoint
- **Fields**: `Id`, `Username`, `Email`, `Roles`, pagination metadata, etc.

---

## Notes

- Each DTO is **single-purpose**, tied to a specific interaction or use case.
- Avoid sharing DTOs across unrelated flows to prevent coupling.
- Keep DTOs **flat and simple**, no direct domain model references.
- Consider using **AutoMapper** or manual mapping depending on control needed.