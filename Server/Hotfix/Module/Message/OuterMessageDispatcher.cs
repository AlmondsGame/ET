﻿using System;
using ETModel;

namespace ETHotfix
{
	public class OuterMessageDispatcher: IMessageDispatcher
	{
		public async void Dispatch(Session session, Packet packet)
		{
			ushort opcode = packet.Opcode();
			Type messageType = session.Network.Entity.GetComponent<OpcodeTypeComponent>().GetType(opcode);
			object message = session.Network.MessagePacker.DeserializeFrom(messageType, packet.Bytes, Packet.Index, packet.Length - Packet.Index);
			
			switch (message)
			{
				case IFrameMessage iFrameMessage: // 如果是帧消息，构造成OneFrameMessage发给对应的unit
				{
					long unitId = session.GetComponent<SessionPlayerComponent>().Player.UnitId;
					ActorProxy actorProxy = Game.Scene.GetComponent<ActorProxyComponent>().Get(unitId);

					// 这里设置了帧消息的id，防止客户端伪造
					iFrameMessage.Id = unitId;

					OneFrameMessage oneFrameMessage = new OneFrameMessage
					{
						Op = opcode,
						AMessage = session.Network.MessagePacker.SerializeToByteArray(iFrameMessage)
					};
					actorProxy.Send(oneFrameMessage);
					return;
				}
				case IActorMessage _: // gate session收到actor消息直接转发给actor自己去处理
				{
					long unitId = session.GetComponent<SessionPlayerComponent>().Player.UnitId;
					ActorProxy actorProxy = Game.Scene.GetComponent<ActorProxyComponent>().Get(unitId);
					actorProxy.Send((IMessage)message);
					return;
				}
				case IActorRequest aActorRequest: // gate session收到actor rpc消息，先向actor 发送rpc请求，再将请求结果返回客户端
				{
					long unitId = session.GetComponent<SessionPlayerComponent>().Player.UnitId;
					ActorProxy actorProxy = Game.Scene.GetComponent<ActorProxyComponent>().Get(unitId);
					IResponse response = await actorProxy.Call(aActorRequest);
					session.Reply(response);
					return;
				}
			}

			if (message != null)
			{
				Game.Scene.GetComponent<MessageDispatherComponent>().Handle(session, new MessageInfo(opcode, message));
				return;
			}

			throw new Exception($"message type error: {message.GetType().FullName}");
		}
	}
}