namespace TelegramAutoChat.Core;

/// <summary>대상 방에서 들어온(또는 내가 보낸) 메시지 1건.</summary>
/// <param name="FromId">발신자 user id (그룹에서 누가 보냈는지). 비공개/단순 메시지는 상대 id.</param>
/// <param name="Id">메시지 id (중복 처리 방지용으로 유용).</param>
/// <param name="Outgoing">내가 보낸 메시지면 true. 보통 false 만 처리하면 된다.</param>
/// <param name="Text">메시지 텍스트.</param>
public sealed record IncomingMessage(long FromId, int Id, bool Outgoing, string Text);
